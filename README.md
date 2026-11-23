# AtemKey

PC のキーボードで Blackmagic Design ATEM スイッチャーの入力 1〜8 を切り替える、小さな Windows アプリです。
ハードウェアコントローラーの代わりに、テンキーやマクロパッドで「押した感」のあるスイッチングができます。

Blackmagic 公式の ATEM Switchers SDK を使用しています。ATEM Production Studio 4K で開発・動作確認しています。

## ダウンロード

[Releases](../../releases) から `AtemKey.zip` をダウンロードし、展開して `AtemKey.exe` を起動してください。.NET のインストールは不要です。

**必要なもの**
- Windows 10 / 11 (64bit)
- [ATEM Software Control](https://www.blackmagicdesign.com/support/)（ATEM Switchers ソフトウェア）がインストールされていること
- ATEM 本体と PC が LAN で接続されていること

## 使い方

1. 上部の「ATEM IP」に本体の IP アドレスを入力して「接続」を押す
2. 画面下に「● 接続済み」と表示されたら準備完了

| キー | 動作 |
|---|---|
| 1〜8 / テンキー 1〜8 | PROGRAM を切り替え |
| Shift + 1〜8 | PREVIEW を選択 |
| Space / テンキー 0 | CUT |
| Enter | AUTO |

「テンキーをどこでも有効」にチェックすると、他のアプリを操作中でもテンキー（1〜8 / 0 / Enter）で切り替えられます。チェック中、これらのキーは他のアプリに入力されません。

PROGRAM は赤、PREVIEW は緑で表示され、ATEM Software Control など他の操作での切り替えも反映されます。接続が切れた場合は 5 秒ごとに自動で再接続します。

## ビルド

必要なもの:
- .NET 10 SDK
- ATEM Software Control
- TlbImp.exe（.NET Framework 4.8 Developer Pack または Visual Studio に含まれます）

```bash
dotnet build -c Release
```

初回ビルド時に `scripts/generate-interop.ps1` が実行され、インストール済みの `BMDSwitcherAPI64.dll` から `lib/Interop.BMDSwitcherAPI.dll` が生成されます（Blackmagic の SDK から派生するファイルのため、リポジトリには含めていません）。

配布用の単一 exe:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o dist/AtemKey
```

## 注意

本ソフトウェアは Blackmagic Design 社の公式製品ではありません。ATEM は Blackmagic Design Pty. Ltd. の商標です。
