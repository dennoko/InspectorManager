# Inspector Manager

UnityのInspectorウィンドウを効率的に管理するエディタ拡張機能。

## 機能

### ローテーションロック
複数のInspectorタブを開いた状態で、オブジェクト選択時に自動的にInspectorを更新。2つの更新モードに対応しています。

- **履歴モード（デフォルト）**: インスペクタリストの上から順に履歴を割り当て（リスト1番目＝最新、2番目＝1つ前...）。ウィンドウ番号（#1, #2...）は固定です。
- **サイクルモード**: 最も古い更新時刻のウィンドウから順に更新

### Inspector管理
- ＋ボタンでInspectorウィンドウを追加（自動でローテーション対象に）
- ✕ボタンで不要なInspectorを閉じる
- ドラッグ&ドロップでローテーションの役割順序（最新、1つ前...）を並び替え
- 個別Inspectorをローテーションから除外/復帰

### ロック管理
- 個別Inspectorのロック/アンロック切り替え
- 全Inspectorの一括ロック/解除
- ホットキーによるクイック操作（ショートカット設定で自由にカスタマイズ可能）
  - `Ctrl+L` - アクティブInspectorのロック切り替え

### ローテーション一時停止
ローテーション有効中に一時停止/再開が可能。一時停止中はオブジェクトを選択してもInspectorが更新されません。

### 選択履歴
- 選択したオブジェクトの履歴を自動記録
- 履歴からワンクリックでオブジェクト選択
- 履歴からドラッグ＆ドロップ可能
- ブラウザのような戻る/進む操作

### お気に入り
- よく使うオブジェクトをお気に入り登録
- プロジェクト再開後も保持
- ドラッグ＆ドロップによる並び替え

### Inspector更新ブロック
フォルダやスクリプトなど、Inspector表示が不要なオブジェクトの選択時に更新をブロック。カテゴリ別に細かく設定可能。

## インストール

1. `Assets/Editor/InspectorManager` フォルダをプロジェクトにコピー
2. Unityエディタを再起動

## 使い方

1. メニューから `dennokoworks > Inspector Manager` を選択してウィンドウを開く
2. ローテーションロックを有効にするには「ローテーション」トグルをONにする
3. 複数のInspectorタブを開いた状態でオブジェクトを選択すると自動的に更新
4. 設定タブで更新モードやショートカットをカスタマイズ

## アーキテクチャ

```
Assets/Editor/InspectorManager/
├── Core/                    # 基盤機能
│   ├── InspectorReflection  # Unity内部API操作
│   ├── EventBus             # イベント通知
│   ├── ServiceLocator       # DI管理
│   └── LifecycleManager     # ライフサイクル管理
├── Models/                  # データモデル
├── Services/                # ビジネスロジック
├── Controllers/             # 機能制御
│   ├── RotationLockController  # ローテーション制御
│   ├── ExclusionManager     # 除外管理
│   ├── HotkeyController     # ショートカット
│   └── SelectionFilter      # 選択フィルタ
├── UI/                      # ユーザーインターフェース
│   ├── InspectorManagerWindow  # メインウィンドウ
│   ├── InspectorStatusView  # 状態タブ
│   ├── HistoryListView      # 履歴タブ
│   ├── FavoritesListView    # お気に入りタブ
│   └── SettingsTabView      # 設定タブ
├── Docs/                    # ドキュメント
└── Resources/Localize/      # 多言語対応（ja/en）
```

## 動作環境

- Unity 2022.3.22f1
- .NET Standard 2.0

## 注意事項

この拡張機能はUnityの内部APIにリフレクションでアクセスするため、Unityのバージョンアップにより動作しなくなる可能性があります。その場合は安全な方式に自動フォールバックします。
