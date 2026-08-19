using System;
using System.Collections.Generic;
using System.Linq;
using InspectorManager.Core;
using InspectorManager.Models;
using InspectorManager.Services;
using UnityEditor;
using UnityEngine;

namespace InspectorManager.Controllers
{
    /// <summary>
    /// ローテーションロック機能の制御。
    ///
    /// 対象のInspectorは常にロック状態に保ち、選択変更時に
    /// ActiveEditorTracker へ直接表示対象を書き込んで更新する。
    /// この内部APIが使えない環境では、対象を一瞬アンロックして
    /// Unity 自身に反映させる旧方式へフォールバックする。
    ///
    /// どのInspectorに何を表示するかは IRotationStrategy が決め、
    /// 本クラスはロック制御・状態の追従・永続化を担う。
    ///
    /// ロック状態の扱いには方向がある：
    /// - 「ユーザーがローテーションから外した」という判断は
    ///   InspectorLockChangedEvent（拡張のUI・ショートカット経由）でのみ行う。
    /// - 定期同期（Reconcile）で見つけたアンロックは意図の推測に使わず、
    ///   ロックを貼り直して復旧させる。
    /// ドメインリロードやプレイモードの往復では ActiveEditorTracker が
    /// 作り直されてロックが落ちることがあり、これを「ユーザーの意図」と
    /// 誤認するとInspectorが次々とローテーションから脱落するため。
    /// </summary>
    public class RotationLockController : IRotationContext, IDisposable
    {
        private readonly IInspectorWindowService _inspectorService;
        private readonly IPersistenceService _persistence;

        // 履歴モードの初期表示を埋めるための選択履歴（無くても動作する）
        private readonly IHistoryService _historyService;

        private readonly ExclusionManager _exclusionManager;

        private bool _isEnabled;
        
        // ローテーション順序（EditorWindow参照で管理、インデックス0が更新対象）
        private List<EditorWindow> _rotationOrder = new List<EditorWindow>();

        // 一度でも観測したInspector。新規ウィンドウと「ユーザーがアンロックした既知ウィンドウ」を区別する
        private readonly List<EditorWindow> _knownInspectors = new List<EditorWindow>();

        // ユーザーが手動でアンロックしたInspector。強制再ロックせず選択追従に任せる。
        // ここに入るのは InspectorLockChangedEvent（＝拡張のUI/ショートカット経由の
        // 明示的な操作）を受け取ったときだけで、ロック状態のポーリング結果からは
        // 推測しない。ドメインリロードやプレイモード往復でトラッカーが作り直されると
        // ロックが落ちることがあり、それを「ユーザーの意図」と誤認すると
        // Inspectorが次々とローテーションから脱落してしまうため。
        private readonly List<EditorWindow> _userUnlocked = new List<EditorWindow>();

        // 自分でロック状態を書き込んでいる間は true。
        // IInspectorWindowService.SetLocked は変更時にイベントを発行するので、
        // 自分の書き込みをユーザー操作と取り違えないように抑止する。
        private bool _applyingLock;

        // 更新中に届いて処理できなかった選択。更新完了後に取りこぼさず反映する
        private UnityEngine.Object[] _deferredSelection;
        private bool _deferredIsNavigation;

        // 明示的な配置の組み直しを予約中。
        // ライフサイクル復帰時の「表示から内部状態を作り直す」処理に
        // 上書きさせないための目印
        private bool _layoutApplyQueued;

        // レガシー経路で更新のため一時的にアンロック中のInspector
        private EditorWindow _pendingUnlockTarget;

        // ローテーション開始前にユーザー自身がロックしていたInspector。
        // ローテーション終了時にこの状態へ戻す
        private readonly List<EditorWindow> _preRotationLocked = new List<EditorWindow>();

        // 各Inspectorに最後に割り当てた選択集合。
        // SetObjectsLockedByThisTracker + ForceRebuild は重い操作なので、
        // 内容が変わっていないInspectorは作り直さないための比較に使う
        private readonly Dictionary<EditorWindow, UnityEngine.Object[]> _lastAssigned =
            new Dictionary<EditorWindow, UnityEngine.Object[]>();

        // モードごとの更新戦略（どのInspectorに何を表示するか）
        private IRotationStrategy _strategy = new HistoryRotationStrategy();

        // 更新処理中フラグ
        private bool _isUpdating;
        
        // 最後に認識した選択（無限ループ防止）
        private UnityEngine.Object[] _lastKnownSelection;

        // 履歴の戻る/進むで選択されたオブジェクト。
        // これによる選択変更はローテーションを進めず、現在の表示を差し替えるだけにする
        private UnityEngine.Object _navigationTarget;

        // 状態同期（Reconcile）の実行間隔
        private const double ReconcileInterval = 0.5;
        private double _lastReconcileTime;

        // delayCallタイムアウト管理
        private double _updateStartTime;
        private const double UpdateTimeout = 1.0;
        private bool _disposed;

        private const string SettingsKey = "RotationLockSettings";

        // セッション状態（ドメインリロードを跨いで維持する実行時状態）のキー
        private const string StateKeyRotationOrder = "InspectorManager.RotationOrder";
        private const string StateKeyKnown = "InspectorManager.KnownInspectors";
        private const string StateKeyUserUnlocked = "InspectorManager.UserUnlocked";
        private const string StateKeyExcluded = "InspectorManager.Excluded";
        private const string StateKeyPreRotationLocked = "InspectorManager.PreRotationLocked";

        /// <summary>
        /// 現在のローテーションモード。
        /// 変更すると対応する戦略へ差し替え、旧モードの内部状態は破棄する。
        /// </summary>
        public RotationMode Mode
        {
            get => _strategy.Mode;
            set
            {
                if (_strategy.Mode == value) return;

                _strategy.Reset();
                _strategy = value == RotationMode.History
                    ? (IRotationStrategy)new HistoryRotationStrategy()
                    : new CycleRotationStrategy();

                // どのInspectorに何を出すかの対応が変わるため、
                // 「内容が同じなら再構築しない」判定に使うキャッシュは捨てる
                _lastAssigned.Clear();

                if (!_isEnabled) return;

                _strategy.Seed(BuildSeedHistory());
                // このセッターは設定タブの描画中から呼ばれうる。
                // Inspectorの作り直しをGUIパスの中で走らせないよう遅延させる。
                ScheduleApplyLayout();
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    SaveSettings();

                    if (_isEnabled)
                    {
                        _isPaused = false;
                        InitializeRotation();
                    }
                    else
                    {
                        _isPaused = false;
                        // 一括アンロックすると、ユーザーが元々ロックしていたInspectorまで
                        // 巻き込んで解除してしまう。開始前のスナップショットへ戻す。
                        RestorePreRotationLockStates();
                        _rotationOrder.Clear();
                        _knownInspectors.Clear();
                        _userUnlocked.Clear();
                        _lastAssigned.Clear();
                        _strategy.Reset();
                        _exclusionManager.Clear();
                        SaveRuntimeState();
                    }

                    EventBus.Instance.Publish(new RotationLockStateChangedEvent { IsEnabled = _isEnabled });
                }
            }
        }
        
        public bool AutoFocusOnUpdate { get; set; }

        private bool _isPaused;
        /// <summary>
        /// ローテーションの一時停止。ONのまま更新だけを止める
        /// </summary>
        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                if (_isPaused != value)
                {
                    _isPaused = value;
                    EventBus.Instance.Publish(new RotationPauseChangedEvent { IsPaused = _isPaused });
                }
            }
        }

        /// <summary>
        /// ブロックフィルタリング用の設定参照
        /// </summary>
        public InspectorManagerSettings FilterSettings { get; set; }

        /// <summary>
        /// 設定オブジェクトから派生する状態をまとめて反映する。
        /// 設定インスタンスが差し替わる場合（リセット等）でも取りこぼしが出ないよう、
        /// 設定の伝播は必ずこのメソッドを経由させること。
        /// </summary>
        public void ApplySettings(InspectorManagerSettings settings)
        {
            if (settings == null) return;

            FilterSettings = settings;
            AutoFocusOnUpdate = settings.AutoFocusOnUpdate;
            Mode = settings.RotationMode;
        }

        public bool IsNextTarget(EditorWindow inspector)
        {
            if (_rotationOrder.Count == 0) return false;
            return _rotationOrder[0] == inspector;
        }

        public RotationLockController(
            IInspectorWindowService inspectorService,
            IPersistenceService persistence,
            IHistoryService historyService = null)
        {
            _inspectorService = inspectorService;
            _persistence = persistence;
            _historyService = historyService;
            _exclusionManager = new ExclusionManager(inspectorService);

            LoadSettings();
            RestoreRuntimeState();
            // Selection の購読は SelectionCoordinator に集約されている
            EventBus.Instance.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
            EventBus.Instance.Subscribe<HistoryNavigationEvent>(OnHistoryNavigation);
            // ロック状態の「ユーザーによる変更」はイベントで受け取る（推測しない）
            EventBus.Instance.Subscribe<InspectorLockChangedEvent>(OnInspectorLockChanged);

            // 状態の同期はこのコントローラ自身が所有するティックからのみ行う。
            // 以前は描画パスやオーバーレイの更新から呼ばれており、
            // 「誰が状態を変えるのか」が追いにくくなっていた。
            EditorApplication.update += OnEditorUpdate;

            // プレイモードの往復ではInspectorのトラッカーが作り直され、
            // ロックが落ちたまま復帰することがある。復帰後にロックを貼り直す。
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            // このコンストラクタはドメインリロード直後にも走る。
            // Inspector 側の OnEnable / RestoreLockStateFromSerializedData が
            // 終わってからでないと状態を読めないため、1フレーム遅らせて貼り直す。
            ScheduleReassert();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            EventBus.Instance.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
            EventBus.Instance.Unsubscribe<HistoryNavigationEvent>(OnHistoryNavigation);
            EventBus.Instance.Unsubscribe<InspectorLockChangedEvent>(OnInspectorLockChanged);
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            _deferredSelection = null;
            _rotationOrder.Clear();
            _knownInspectors.Clear();
            _userUnlocked.Clear();
            _preRotationLocked.Clear();
            _lastAssigned.Clear();
            _exclusionManager.Clear();
            _pendingUnlockTarget = null;
            _isUpdating = false;
        }

        public void InitializeRotation()
        {
            _rotationOrder.Clear();
            _userUnlocked.Clear();
            _knownInspectors.Clear();
            _preRotationLocked.Clear();
            _lastAssigned.Clear();
            _strategy.Reset();
            _exclusionManager.CleanupInvalid();

            var inspectors = _inspectorService.GetAllInspectors();

            // ローテーション終了時に元へ戻せるよう、開始前のロック状態を記録しておく
            foreach (var inspector in inspectors)
            {
                if (_inspectorService.IsLocked(inspector))
                {
                    _preRotationLocked.Add(inspector);
                }
            }

            foreach (var inspector in inspectors)
            {
                _knownInspectors.Add(inspector);
                // 除外中のInspectorはロックだけ維持し、ローテーション対象には含めない。
                // （以前は無条件に追加していたため、OFF→ONするだけで除外設定が壊れていた）
                SetInspectorLocked(inspector, true);

                if (_exclusionManager.IsExcluded(inspector)) continue;

                _rotationOrder.Add(inspector);
            }

            _lastKnownSelection = Selection.objects;

            // ロックした時点で各Inspectorが何を表示しているかを実際に読み取る。
            // 元々ロックされていたInspectorは現在の選択とは別のものを表示しているため、
            // 「現在の選択で埋める」と以後その内容への更新が
            // 「変化なし」と判定されて反映されなくなる。
            var buffer = new List<UnityEngine.Object>();
            foreach (var inspector in _rotationOrder)
            {
                if (InspectorReflection.TryGetLockedObjects(inspector, buffer) && buffer.Count > 0)
                {
                    _lastAssigned[inspector] = buffer.ToArray();
                }
            }

            // 開始直後は全Inspectorが同じ対象（現在の選択）を表示している。
            // 直近の選択履歴を種として渡し、2番目以降にそれを割り当てることで
            // 「2番目と3番目が同じ」状態を解消する。
            _strategy.Seed(BuildSeedHistory());
            ScheduleApplyLayout();

            SaveRuntimeState();
        }

        public void SetExcluded(EditorWindow inspector, bool isExcluded)
        {
            _exclusionManager.SetExcluded(inspector, isExcluded, _rotationOrder, Reconcile);
            SaveRuntimeState();
        }

        /// <summary>
        /// ローテーション開始前のロック状態へ戻す。
        /// 開始後に作られたInspectorはスナップショットに含まれないためアンロックされる。
        /// </summary>
        private void RestorePreRotationLockStates()
        {
            var inspectors = _inspectorService.GetAllInspectors();
            foreach (var inspector in inspectors)
            {
                SetInspectorLocked(inspector, _preRotationLocked.Contains(inspector));
            }
            _preRotationLocked.Clear();
        }

        /// <summary>
        /// ドメインリロードを跨いで実行時状態（ローテーション順序・除外・手動アンロック）を復元する。
        /// これを行わないと、スクリプト再コンパイルやプレイモードの往復のたびに
        /// ユーザーがドラッグ＆ドロップで組んだ役割の並びが失われてしまう。
        /// </summary>
        private void RestoreRuntimeState()
        {
            var inspectors = _inspectorService.GetAllInspectors();

            _rotationOrder.Clear();
            _rotationOrder.AddRange(WindowStateStore.Load(StateKeyRotationOrder, inspectors));

            _knownInspectors.Clear();
            _knownInspectors.AddRange(WindowStateStore.Load(StateKeyKnown, inspectors));

            _userUnlocked.Clear();
            _userUnlocked.AddRange(WindowStateStore.Load(StateKeyUserUnlocked, inspectors));

            _exclusionManager.Restore(WindowStateStore.Load(StateKeyExcluded, inspectors));

            _preRotationLocked.Clear();
            _preRotationLocked.AddRange(WindowStateStore.Load(StateKeyPreRotationLocked, inspectors));

            // セッション状態が無い（エディタ再起動、またはウィンドウを閉じて開き直した）
            // 場合は、ローテーションを一から初期化する。
            // ここで初期化しないと、アンロック状態のInspectorが
            // 「ユーザーが手動で解除したもの」と誤判定されてしまう。
            if (_isEnabled && _knownInspectors.Count == 0)
            {
                InitializeRotation();
                return;
            }

            _lastKnownSelection = Selection.objects;
        }

        /// <summary>
        /// Inspector Manager ウィンドウが閉じられたときに呼ぶ。
        /// ローテーションを休止し、Inspectorのロック状態を開始前へ戻す。
        ///
        /// ウィンドウを閉じると Selection の購読が外れてローテーションは止まるが、
        /// 従来は全Inspectorがロックされたまま取り残され、
        /// 「Inspectorが一切更新されない」状態の原因が分からなくなっていた。
        ///
        /// OnDisable の時点でコントローラは破棄済みのため、
        /// セッション状態から直接復元する静的メソッドとして提供する。
        /// 設定上の有効フラグは変更しないので、再度ウィンドウを開けば復帰する。
        /// </summary>
        public static void SuspendForWindowClose(IInspectorWindowService inspectorService)
        {
            if (inspectorService == null || !inspectorService.IsAvailable) return;

            var inspectors = inspectorService.GetAllInspectors();

            // ローテーションが一度も動いていなければ何もしない
            var known = WindowStateStore.Load(StateKeyKnown, inspectors);
            if (known.Count == 0) return;

            var preLocked = WindowStateStore.Load(StateKeyPreRotationLocked, inspectors);
            foreach (var inspector in inspectors)
            {
                inspectorService.SetLocked(inspector, preLocked.Contains(inspector));
            }

            WindowStateStore.Clear(StateKeyRotationOrder);
            WindowStateStore.Clear(StateKeyKnown);
            WindowStateStore.Clear(StateKeyUserUnlocked);
            WindowStateStore.Clear(StateKeyExcluded);
            WindowStateStore.Clear(StateKeyPreRotationLocked);
        }

        /// <summary>
        /// 実行時状態をセッションに保存する。SessionState はメモリ上のマップなので
        /// 高頻度で呼んでも問題ない。
        /// </summary>
        private void SaveRuntimeState()
        {
            WindowStateStore.Save(StateKeyRotationOrder, _rotationOrder);
            WindowStateStore.Save(StateKeyKnown, _knownInspectors);
            WindowStateStore.Save(StateKeyUserUnlocked, _userUnlocked);
            WindowStateStore.Save(StateKeyExcluded, _exclusionManager.GetExcluded());
            WindowStateStore.Save(StateKeyPreRotationLocked, _preRotationLocked);
        }

        public bool IsExcluded(EditorWindow inspector)
        {
            return _exclusionManager.IsExcluded(inspector);
        }

        public void GetInspectorLists(out List<EditorWindow> rotation, out List<EditorWindow> excluded, out List<EditorWindow> unmanaged)
        {
            var all = _inspectorService.GetAllInspectors();
            rotation = new List<EditorWindow>(_rotationOrder);
            excluded = new List<EditorWindow>();
            unmanaged = new List<EditorWindow>();

            foreach (var w in all)
            {
                if (IsExcluded(w))
                {
                    excluded.Add(w);
                }
                else if (!_rotationOrder.Contains(w))
                {
                    unmanaged.Add(w);
                }
            }
        }

        /// <summary>
        /// 定期ティック。状態の変更はここか、選択変更・明示的な操作からのみ行う。
        /// </summary>
        private void OnEditorUpdate()
        {
            if (_disposed || !_isEnabled) return;

            // 保留された選択は間隔を待たずに反映する
            if (_deferredSelection != null && !_isUpdating)
            {
                FlushDeferredSelection();
                return;
            }

            if (EditorApplication.timeSinceStartup - _lastReconcileTime < ReconcileInterval) return;
            _lastReconcileTime = EditorApplication.timeSinceStartup;

            Reconcile();
        }

        /// <summary>
        /// Inspectorの開閉やユーザーによるロック変更を検出し、内部状態を追従させる。
        ///
        /// これは状態を変更するメソッドである。UIの描画パスから呼んではならない
        /// （Layout/Repaint 間で行数が変わり EditorGUILayout が例外を出す）。
        /// 表示側は GetInspectorLists() で読み取りのみ行うこと。
        /// </summary>
        public void Reconcile()
        {
            var currentInspectors = _inspectorService.GetAllInspectors();

            // 閉じられたInspectorを各リストから除去
            bool changed = _rotationOrder.RemoveAll(i => !currentInspectors.Contains(i)) > 0;
            changed |= _knownInspectors.RemoveAll(i => !currentInspectors.Contains(i)) > 0;
            changed |= _userUnlocked.RemoveAll(i => !currentInspectors.Contains(i)) > 0;
            changed |= _exclusionManager.CleanupInvalid() > 0;
            CleanupLastAssigned(currentInspectors);

            if (_pendingUnlockTarget != null && !currentInspectors.Contains(_pendingUnlockTarget))
            {
                _pendingUnlockTarget = null;
            }

            // タイムアウトチェック。以降のロック判定を正しい状態で行うため先に処理する
            if (_isUpdating && (EditorApplication.timeSinceStartup - _updateStartTime) > UpdateTimeout)
            {
                Debug.LogWarning("[InspectorManager] Rotation update timed out, resetting state.");
                if (_pendingUnlockTarget != null)
                {
                    // 一時アンロックしたまま取り残されないよう確実に戻す
                    SetInspectorLocked(_pendingUnlockTarget, true);
                    _pendingUnlockTarget = null;
                }
                _isUpdating = false;
            }

            foreach (var inspector in currentInspectors)
            {
                if (_exclusionManager.IsExcluded(inspector)) continue;

                // 更新のため一時的にアンロック中のものには触らない
                if (_isUpdating && inspector == _pendingUnlockTarget) continue;

                // 初めて観測したInspectorはロックしてローテーションに参加させる
                if (!_knownInspectors.Contains(inspector))
                {
                    _knownInspectors.Add(inspector);
                    SetInspectorLocked(inspector, true);
                    if (!_rotationOrder.Contains(inspector)) _rotationOrder.Add(inspector);
                    changed = true;
                    continue;
                }

                // ユーザーが明示的にアンロックしたものは選択追従に任せる
                if (_userUnlocked.Contains(inspector)) continue;

                if (!_rotationOrder.Contains(inspector))
                {
                    _rotationOrder.Add(inspector);
                    changed = true;
                }

                // ローテーション対象は常にロック状態に保つ。
                // ここでロックが外れているのはユーザー操作ではなく
                // ライフサイクル（ドメインリロード等）でトラッカーが作り直された結果なので、
                // 状態を推測せずロックを貼り直す。
                // 貼り直さないとそのInspectorだけ選択に追従してしまい、
                // 「複数のInspectorが同じ最新の選択を表示する」状態になる。
                if (!_inspectorService.IsLocked(inspector))
                {
                    SetInspectorLocked(inspector, true);
                    // Unity側が表示対象を差し替えている可能性があるためキャッシュを捨てる
                    _lastAssigned.Remove(inspector);
                }
            }

            // 毎ティック書き込まないよう、実際に変化があったときだけ保存する
            if (changed) SaveRuntimeState();
        }

        /// <summary>
        /// ロック状態を書き込む。自分の書き込みが
        /// InspectorLockChangedEvent としてユーザー操作と誤認されないよう抑止する。
        /// </summary>
        private void SetInspectorLocked(EditorWindow inspector, bool locked)
        {
            _applyingLock = true;
            try
            {
                _inspectorService.SetLocked(inspector, locked);
            }
            finally
            {
                _applyingLock = false;
            }
        }

        /// <summary>
        /// ロック状態の変更通知。拡張のUI・ショートカット経由の操作だけがここに届く。
        ///
        /// 「ユーザーがアンロックした」という判断はこの経路でのみ行う。
        /// 定期的なポーリングでアンロックを検出して同じ判断をすると、
        /// ドメインリロードやプレイモード往復でロックが落ちただけの状況を
        /// ユーザーの意図と取り違え、Inspectorがローテーションから脱落してしまう。
        /// </summary>
        private void OnInspectorLockChanged(InspectorLockChangedEvent evt)
        {
            if (_disposed || !_isEnabled) return;
            if (_applyingLock) return;

            var inspector = evt.Inspector;
            if (inspector == null) return;
            if (_exclusionManager.IsExcluded(inspector)) return;

            if (evt.IsLocked)
            {
                // 再ロック：ローテーションの末尾に復帰させる
                if (!_userUnlocked.Remove(inspector)) return;

                if (!_knownInspectors.Contains(inspector)) _knownInspectors.Add(inspector);
                if (!_rotationOrder.Contains(inspector)) _rotationOrder.Add(inspector);
            }
            else
            {
                // 明示的なアンロック：ローテーションから外して通常の選択追従に戻す
                if (!_userUnlocked.Contains(inspector)) _userUnlocked.Add(inspector);
                _rotationOrder.Remove(inspector);
                _lastAssigned.Remove(inspector);
            }

            SaveRuntimeState();
        }

        /// <summary>
        /// 戦略に渡す初期履歴を組み立てる（新しい順）。
        ///
        /// 先頭は現在の選択。以降は拡張が記録している選択履歴から新しい順に拾う。
        /// 履歴モードでは開始直後に全Inspectorが同じ対象を表示しているため、
        /// これを渡さないと履歴が溜まるまで2番目以降が同じ表示のままになる。
        /// </summary>
        private List<UnityEngine.Object[]> BuildSeedHistory()
        {
            var result = new List<UnityEngine.Object[]>();

            // Inspectorの数だけあれば足りる
            int wanted = Mathf.Max(_rotationOrder.Count, 1);

            var current = Selection.objects;
            if (current != null && current.Length > 0) result.Add(current);

            // 「1つ前 / 2つ前」の意味に一番近いのは記録済みの選択履歴
            if (_historyService != null)
            {
                var entries = _historyService.GetHistory();
                for (int i = entries.Count - 1; i >= 0 && result.Count < wanted; i--)
                {
                    var obj = entries[i]?.GetObject();
                    if (obj == null) continue;
                    if (ContainsAny(result, obj)) continue;

                    result.Add(new[] { obj });
                }
            }

            // それでも足りなければ、各Inspectorが今表示している内容で埋める。
            // ローテーション開始前から個別にロックされていたInspectorは
            // 現在の選択とは別のものを表示しているので、それを活かせる。
            if (result.Count < wanted)
            {
                var buffer = new List<UnityEngine.Object>();
                foreach (var inspector in _rotationOrder)
                {
                    if (result.Count >= wanted) break;
                    if (!InspectorReflection.TryGetLockedObjects(inspector, buffer)) continue;
                    if (buffer.Count == 0) continue;

                    var objects = buffer.ToArray();
                    if (ContainsAny(result, objects)) continue;

                    result.Add(objects);
                }
            }

            return result;
        }

        private static bool ContainsAny(List<UnityEngine.Object[]> entries, UnityEngine.Object obj)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                for (int j = 0; j < entry.Length; j++)
                {
                    if (entry[j] == obj) return true;
                }
            }
            return false;
        }

        private static bool ContainsAny(List<UnityEngine.Object[]> entries, UnityEngine.Object[] objects)
        {
            for (int i = 0; i < objects.Length; i++)
            {
                if (ContainsAny(entries, objects[i])) return true;
            }
            return false;
        }

        /// <summary>
        /// 次のエディタ更新で、戦略が持つ現在の対応をInspectorへ反映する。
        /// ローテーションの開始・モード切替はGUIの描画中に呼ばれうるため遅延させる。
        /// </summary>
        private void ScheduleApplyLayout()
        {
            // 明示的に組み直した配置は、後続の再同期に上書きさせない
            _layoutApplyQueued = true;

            EditorApplication.delayCall += () =>
            {
                _layoutApplyQueued = false;

                if (_disposed || !_isEnabled || _isPaused) return;
                if (_isUpdating) return;
                if (_rotationOrder.Count == 0) return;
                if (!InspectorReflection.IsDirectUpdateAvailable) return;

                _isUpdating = true;
                _updateStartTime = EditorApplication.timeSinceStartup;

                try
                {
                    _strategy.ApplyLayout(this);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[InspectorManager] Rotation layout failed: {ex.Message}");
                }
                finally
                {
                    _isUpdating = false;
                }
            };
        }

        /// <summary>
        /// 次のエディタ更新でロック状態を貼り直す。
        /// Inspector 側の OnEnable / RestoreLockStateFromSerializedData より
        /// 後に走らせる必要があるため遅延させる。
        /// </summary>
        private void ScheduleReassert()
        {
            EditorApplication.delayCall += () =>
            {
                if (_disposed || !_isEnabled) return;
                ReassertLockStates();
            };
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (_disposed) return;
            if (state != PlayModeStateChange.EnteredEditMode && state != PlayModeStateChange.EnteredPlayMode) return;

            ScheduleReassert();
        }

        /// <summary>
        /// ローテーション対象のロックを貼り直し、表示中の内容を実際の状態から読み直す。
        ///
        /// ドメインリロードやプレイモードの往復では ActiveEditorTracker が
        /// 作り直され、ロックが外れたまま復帰することがある。
        /// この状態を放置すると、そのInspectorは選択に追従してしまい
        /// ローテーションが崩れる。
        /// </summary>
        private void ReassertLockStates()
        {
            var currentInspectors = _inspectorService.GetAllInspectors();
            var buffer = new List<UnityEngine.Object>();

            foreach (var inspector in currentInspectors)
            {
                if (inspector == null) continue;
                if (_userUnlocked.Contains(inspector)) continue;
                if (!_rotationOrder.Contains(inspector) && !_exclusionManager.IsExcluded(inspector)) continue;

                if (!_inspectorService.IsLocked(inspector))
                {
                    SetInspectorLocked(inspector, true);
                }

                // リロードを跨いだ後の割り当てキャッシュは信用できない。
                // Inspectorが実際に保持している対象で作り直す。
                if (InspectorReflection.TryGetLockedObjects(inspector, buffer) && buffer.Count > 0)
                {
                    _lastAssigned[inspector] = buffer.ToArray();
                }
                else
                {
                    _lastAssigned.Remove(inspector);
                }
            }

            ReseedStrategyFromDisplay();
        }

        /// <summary>
        /// 実際にInspectorが表示している内容から戦略の内部状態を作り直す。
        ///
        /// ドメインリロードを跨ぐと戦略は新しいインスタンスになり、履歴が空になる。
        /// そのまま動かすと、履歴が溜まるまで後ろのInspectorが更新されず
        /// リロード前の内容と重複したままになる。
        /// 表示は Unity 側が復元しているので、それをそのまま内部状態にする。
        /// </summary>
        private void ReseedStrategyFromDisplay()
        {
            // 明示的に組み直した配置を待っている最中は触らない
            if (_layoutApplyQueued) return;
            if (_rotationOrder.Count == 0) return;

            var display = new List<UnityEngine.Object[]>(_rotationOrder.Count);
            foreach (var inspector in _rotationOrder)
            {
                if (_lastAssigned.TryGetValue(inspector, out var objects) && objects != null && objects.Length > 0)
                {
                    display.Add(objects);
                }
            }

            if (display.Count == 0) return;

            _strategy.Seed(display);
        }

        /// <summary>
        /// 閉じられたInspectorを割り当てキャッシュから除去する
        /// </summary>
        private void CleanupLastAssigned(IReadOnlyList<EditorWindow> currentInspectors)
        {
            if (_lastAssigned.Count == 0) return;

            List<EditorWindow> stale = null;
            foreach (var kvp in _lastAssigned)
            {
                if (currentInspectors.Contains(kvp.Key)) continue;
                (stale ?? (stale = new List<EditorWindow>())).Add(kvp.Key);
            }

            if (stale == null) return;
            foreach (var inspector in stale)
            {
                _lastAssigned.Remove(inspector);
            }
        }

        /// <summary>
        /// Inspectorに選択集合を割り当てる。
        /// 既に同じ内容を表示している場合は ForceRebuild を避けるためスキップする。
        /// </summary>
        private bool AssignToInspector(EditorWindow inspector, UnityEngine.Object[] objects)
        {
            if (inspector == null || objects == null || objects.Length == 0) return false;

            if (_lastAssigned.TryGetValue(inspector, out var current)
                && IsSameSelection(current, objects))
            {
                return true;
            }

            if (InspectorReflection.SetInspectedObjects(inspector, objects))
            {
                _lastAssigned[inspector] = objects;
                return true;
            }

            _lastAssigned.Remove(inspector);
            return false;
        }

        /// <summary>
        /// ユーザーが手動でアンロックし、ローテーションから外れているかどうか
        /// </summary>
        public bool IsUserUnlocked(EditorWindow inspector)
        {
            return inspector != null && _userUnlocked.Contains(inspector);
        }

        private void OnSelectionChanged(SelectionChangedEvent evt)
        {
            if (!_isEnabled) return;
            if (_isPaused) return;

            // Unity標準と同じマルチ編集表示を再現するため、選択集合全体を扱う
            var newSelection = evt.SelectedObjects;
            if (newSelection == null || newSelection.Length == 0)
            {
                // 選択が解除された。記録を消しておかないと、同じオブジェクトを
                // 選び直したときに「変化なし」と判定されて更新がスキップされる。
                _lastKnownSelection = null;
                return;
            }

            // ブロックフィルタ判定
            if (FilterSettings != null && SelectionFilter.ShouldBlock(newSelection, FilterSettings))
            {
                return;
            }

            // 同じ選択の重複通知は無視する。
            // ここで打ち切る場合は _navigationTarget をまだ消費してはならない。
            // Unity は選択操作の途中で一度前の選択のまま通知することがあり、
            // 先に消費すると本命のナビゲーション通知を通常の選択と誤認して
            // 履歴が余計に積まれる／ローテーションが進んでしまう。
            if (IsSameSelection(newSelection, _lastKnownSelection)) return;

            // 履歴ナビゲーション由来かどうかを判定（フラグは1回の選択変更で消費する）
            bool isNavigation = _navigationTarget != null
                && newSelection.Length == 1
                && newSelection[0] == _navigationTarget;
            _navigationTarget = null;

            _lastKnownSelection = newSelection;

            // レガシー経路の更新は delayCall を跨ぐため、その間に届いた選択は
            // 捨てずに保留しておく。捨てると「選択したのに更新されない」が起きる。
            if (_isUpdating)
            {
                _deferredSelection = newSelection;
                _deferredIsNavigation = isNavigation;
                return;
            }

            Reconcile();
            if (_rotationOrder.Count == 0) return;

            PerformUpdate(newSelection, isNavigation);
        }

        /// <summary>
        /// 更新中に届いて保留された選択を反映する。
        /// </summary>
        private void FlushDeferredSelection()
        {
            if (_disposed || _deferredSelection == null) return;

            var selection = _deferredSelection;
            var isNavigation = _deferredIsNavigation;
            _deferredSelection = null;

            if (!_isEnabled || _isPaused || _isUpdating) return;

            Reconcile();
            if (_rotationOrder.Count == 0) return;

            PerformUpdate(selection, isNavigation);
        }

        /// <summary>
        /// 選択変更をInspectorへ反映する。
        ///
        /// 直接更新が使えるかどうかの判定はここ1箇所だけで行う。
        /// 使えない環境ではアンロック/再ロックによる旧方式しか取れず、
        /// これは「先頭のInspectorが現在の選択を表示する」＝サイクル相当の
        /// 動きに限られるため、モードによらず共通の経路で処理する。
        /// </summary>
        private void PerformUpdate(UnityEngine.Object[] newSelection, bool isNavigation)
        {
            if (!InspectorReflection.IsDirectUpdateAvailable)
            {
                _strategy.Reset();
                PerformLegacyUpdate(newSelection, isNavigation);
                return;
            }

            _isUpdating = true;
            _updateStartTime = EditorApplication.timeSinceStartup;

            try
            {
                _strategy.Apply(this, newSelection, isNavigation);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InspectorManager] Rotation update failed: {ex.Message}");
            }
            finally
            {
                _isUpdating = false;
            }

            FlushDeferredSelection();
        }

        private void OnHistoryNavigation(HistoryNavigationEvent evt)
        {
            _navigationTarget = evt.Target;
        }

        /// <summary>
        /// 2つの選択集合が同一かどうか（順序も含めて比較）
        /// </summary>
        private static bool IsSameSelection(UnityEngine.Object[] a, UnityEngine.Object[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        /// <summary>
        /// ローテーション順序を1つ進める（更新済みのInspectorを末尾へ回す）
        /// </summary>
        private void AdvanceRotation(EditorWindow updated)
        {
            // 非同期経路では待っている間にリストが変化しうるため、
            // 先頭が想定どおりの場合のみ回す（誤って別のInspectorを外さない）
            if (_rotationOrder.Count == 0) return;
            if (_rotationOrder[0] != updated) return;

            _rotationOrder.RemoveAt(0);
            _rotationOrder.Add(updated);
            SaveRuntimeState();
        }

        /// <summary>
        /// 旧方式の更新処理。
        /// 対象Inspectorを一瞬アンロックして Unity 自身に選択を反映させ、
        /// 次のエディタ更新で再ロックする。
        /// 直接更新（SetObjectsLockedByThisTracker）が使えない場合の
        /// フォールバックであり、サイクル相当の動きになる。
        /// </summary>
        /// <param name="isNavigation">
        /// 履歴の戻る/進む由来の場合 true。ローテーションを進めず表示だけ差し替える。
        /// </param>
        private void PerformLegacyUpdate(UnityEngine.Object[] newSelection, bool isNavigation)
        {
            if (_rotationOrder.Count == 0) return;

            _isUpdating = true;
            _updateStartTime = EditorApplication.timeSinceStartup;

            try
            {
                var targetInspector = _rotationOrder[0];

                // 一時アンロック中であることを記録し、Reconcile が
                // 「ユーザーによるアンロック」と誤検出しないようにする
                _pendingUnlockTarget = targetInspector;
                // アンロックするとUnity側が表示対象を差し替えるためキャッシュは無効
                _lastAssigned.Remove(targetInspector);
                SetInspectorLocked(targetInspector, false);
                targetInspector.Repaint();

                EditorApplication.delayCall += () =>
                {
                    if (_disposed) return;

                    SetInspectorLocked(targetInspector, true);
                    _pendingUnlockTarget = null;

                    if (!isNavigation) AdvanceRotation(targetInspector);

                    NotifyUpdated(targetInspector, newSelection);

                    _isUpdating = false;

                    // 待っている間に届いた選択を取りこぼさない
                    FlushDeferredSelection();
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InspectorManager] Rotation update failed: {ex.Message}");
                if (_pendingUnlockTarget != null)
                {
                    SetInspectorLocked(_pendingUnlockTarget, true);
                    _pendingUnlockTarget = null;
                }
                _isUpdating = false;
            }
        }

        /// <summary>
        /// ローテーション順序内のInspectorを並び替える（参照指定）。
        /// ドラッグ中にリスト構成が変化してもインデックスがずれないよう、
        /// UI側はこちらを使う。
        /// </summary>
        public void ReorderInspector(EditorWindow moved, EditorWindow target)
        {
            if (moved == null || target == null) return;

            int fromIndex = _rotationOrder.IndexOf(moved);
            int toIndex = _rotationOrder.IndexOf(target);
            if (fromIndex < 0 || toIndex < 0) return;

            ReorderInspector(fromIndex, toIndex);
        }

        /// <summary>
        /// ローテーション順序内のInspectorを並び替える
        /// </summary>
        public void ReorderInspector(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= _rotationOrder.Count) return;
            if (toIndex < 0 || toIndex >= _rotationOrder.Count) return;
            if (fromIndex == toIndex) return;

            var item = _rotationOrder[fromIndex];
            _rotationOrder.RemoveAt(fromIndex);
            _rotationOrder.Insert(toIndex, item);

            SaveRuntimeState();
        }

        /// <summary>
        /// Inspector Manager経由で生成されたInspectorをローテーションに追加する
        /// </summary>
        public void AddManagedInspector(EditorWindow inspector)
        {
            if (inspector == null) return;

            SetInspectorLocked(inspector, true);

            if (!_knownInspectors.Contains(inspector))
            {
                _knownInspectors.Add(inspector);
            }
            _userUnlocked.Remove(inspector);

            if (!_rotationOrder.Contains(inspector))
            {
                _rotationOrder.Add(inspector);
            }

            // 除外リストから確実に除去
            _exclusionManager.SetExcluded(inspector, false, _rotationOrder, Reconcile);

            SaveRuntimeState();
        }

        // ────────── IRotationContext ──────────

        IReadOnlyList<EditorWindow> IRotationContext.RotationOrder => _rotationOrder;

        bool IRotationContext.Assign(EditorWindow inspector, UnityEngine.Object[] objects)
        {
            return AssignToInspector(inspector, objects);
        }

        void IRotationContext.AdvanceRotation(EditorWindow updated)
        {
            AdvanceRotation(updated);
        }

        void IRotationContext.NotifyUpdated(EditorWindow inspector, UnityEngine.Object[] selection)
        {
            NotifyUpdated(inspector, selection);
        }

        /// <summary>
        /// 更新完了を通知し、必要なら対象Inspectorにフォーカスする
        /// </summary>
        private void NotifyUpdated(EditorWindow inspector, UnityEngine.Object[] selection)
        {
            if (inspector == null) return;

            EventBus.Instance.Publish(new RotationUpdateCompletedEvent
            {
                UpdatedInspector = inspector,
                DisplayedObject = GetPrimary(selection)
            });

            if (AutoFocusOnUpdate && !IsFocusingHierarchyOrProject())
            {
                inspector.Focus();
            }
        }

        /// <summary>
        /// 選択集合の代表オブジェクト（先頭）を返す。UI通知用。
        /// </summary>
        private static UnityEngine.Object GetPrimary(UnityEngine.Object[] selection)
        {
            if (selection == null) return null;

            for (int i = 0; i < selection.Length; i++)
            {
                if (selection[i] != null) return selection[i];
            }
            return null;
        }

        private bool IsFocusingHierarchyOrProject()
        {
            if (EditorWindow.focusedWindow == null) return false;
            var windowType = EditorWindow.focusedWindow.GetType().Name;
            return windowType == "SceneHierarchyWindow" || windowType == "ProjectBrowser";
        }

        private void LoadSettings()
        {
            var settings = _persistence.Load<RotationLockSettings>(SettingsKey, null);
            if (settings != null)
            {
                _isEnabled = settings.IsEnabled;
            }
        }

        private void SaveSettings()
        {
            var settings = new RotationLockSettings { IsEnabled = _isEnabled };
            _persistence.Save(SettingsKey, settings);
        }

        [System.Serializable]
        private class RotationLockSettings
        {
            public bool IsEnabled;
        }
    }
}
