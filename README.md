# Inspector Manager

UnityのInspectorウィンドウを効率的に管理するエディタ拡張機能。

## 機能

### 🔄 ローテーションロック
複数のInspectorタブを開いた状態で、1つのタブだけをアンロック状態にし、オブジェクト選択時に自動的にローテーション。これにより、選択履歴のように過去に選択したオブジェクトを複数のInspectorに表示し続けることができます。

### 🔒 ロック管理
- 個別Inspectorのロック/アンロック切り替え
- 全Inspectorの一括ロック/解除
- ホットキーによるクイック操作
  - `Ctrl+L` - アクティブInspectorのロック切り替え
  - `Ctrl+Shift+L` - 全Inspectorロック切り替え

### 📜 選択履歴
- 選択したオブジェクトの履歴を自動記録
- 履歴からワンクリックでオブジェクト選択
- 履歴からドラッグ＆ドロップ可能
- `Ctrl+[` / `Ctrl+]` - 履歴の前後移動

### ⭐ お気に入り
- よく使うオブジェクトをお気に入り登録
- プロジェクト再開後も保持
- ドラッグ＆ドロップによる並び替え

## インストール

1. `Assets/Editor/InspectorManager` フォルダをプロジェクトにコピー
2. Unityエディタを再起動

## 使い方

1. メニューから `Tools > Inspector Manager` を選択してウィンドウを開く
2. ローテーションロックを有効にするには「ローテーションロック」トグルをONにする
3. 複数のInspectorタブを開いた状態でオブジェクトを選択すると、自動的にローテーション

## アーキテクチャ

```
Assets/Editor/InspectorManager/
├── Core/                    # 基盤機能
│   ├── InspectorReflection  # Unity内部API操作
│   ├── EventBus             # イベント通知
│   └── ServiceLocator       # DI管理
├── Models/                  # データモデル
├── Services/                # ビジネスロジック
├── Controllers/             # 機能制御
└── UI/                      # ユーザーインターフェース
```

## 動作環境

- Unity 2022.3.22f1
- .NET Standard 2.0

## 注意事項

この拡張機能はUnityの内部APIにリフレクションでアクセスするため、Unityのバージョンアップにより動作しなくなる可能性があります。
