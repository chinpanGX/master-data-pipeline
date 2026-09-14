# master-data-pipeline

Google スプレッドシートで入力したマスターデータを、以下の3系統に変換するパイプラインです。

- クライアント(Unity)向け: 暗号化済みバイナリ `masterdata.bytes` ([MasterMemory](https://github.com/Cysharp/MasterMemory))
- リアルタイムサーバー(MagicOnion)向け: クライアントと同一の `masterdata.bytes`
- APIサーバー(Rust、既存の別フロー)向け: `*.rs` / `*.json`

コンバーターツールは `csharp_converter` に1本化されており、クライアント/リアルタイムサーバーの
区別や暗号化オプションはありません。**出力は常に暗号化されます。**

> 現時点ではフォルダ構成とスキーマ定義(Phase 1)のみが整備されています。
> `tools/python` 配下の各スクリプト・`tools/dotnet` 配下のツール本体・`run.sh` / `build.sh` は
> 後続のPhaseで実装されます。

## 全体フロー

```text
Google Sheets(1-2行目コメント、3行目型情報、4行目ヘッダー、5行目〜データ)
  ↓ download_sheets.py
csv/*.csv(生CSV)
  ↓ normalize_csv.py(先頭3行除去)
  ↓ resolve_enum_ids.py(Enum列 name→id 変換)
  ↓ validate_common.py(unique + check_relation 検証)
out/normalized_csv/*.csv(整形・検証済み)
  │
  ├→ csharp-codegen(dotnet, Roslyn)
  │     schema/tables/*.yaml, schema/enums/*.yaml → out/generated_csharp/{Models,Enums}/*.cs
  │
  ├→ csharp_converter(dotnet、常に暗号化)
  │     out/generated_csharp/*.cs + out/normalized_csv/*.csv → out/masterdata.bytes
  │     (MasterDataLoader.cs も生成)
  │
  └→ server_codegen(Rust、既存APIサーバー向け)
        schema/tables/*.yaml(targets: server) → out/generated_rust/*.rs, out/server/*.json

[配置(コピー)スクリプト] out/ の生成物を client / realtime_server / server へコピーする
```

## スプレッドシート行ルール

各シートは以下の固定レイアウトで入力します。

| 行       | 内容                                                                        |
| -------- | --------------------------------------------------------------------------- |
| 1-2行目 | 自由記述のコメント行(コンバーターは読まない)                              |
| 3行目    | 型情報行(コメント用。実際の型は `schema/tables/*.yaml` の `fields[].type` が正) |
| 4行目    | ヘッダー行(列名。`schema/tables/*.yaml` の `fields[].name` と一致させる)  |
| 5行目〜  | データ行                                                                     |

### 対応している型

`schema/tables/*.yaml` の `fields[].type` に指定できるのは次の4種類です。

- `int`
- `string`
- `bool`
- `enum`(`enum_type` で `schema/enums/*.yaml` の `enum` 名を指定)

### targets

`fields[].targets` で、そのフィールドを client(Unity/MagicOnion) / server(Rust API) の
どちらに出力するかを制御します。**省略時のデフォルトは `[client, server]`(両方)**です。
サーバーにしか出さない列(内部用の説明文など)は `targets: [server]` のように明示します。

### Enum

`schema/enums/*.yaml` に `enum`(型名)と `members`(`id` / `name` の組)を定義します。
スプレッドシートのセルには `id` ではなく `name` を入力し、`resolve_enum_ids.py` が
`name → id` に変換します。定義に無い `name` が入力された場合は検証エラーになります。
セカンダリキーは現時点でスキーマから削除されており、将来必要になった時点で再設計します。

## 参照・コピーの原則

- **pipeline内で完結する参照は直接参照する**(例: `csharp_converter` が
  `out/generated_csharp/*.cs` を `Compile Include` で直接参照する、`schema/tables/*.yaml` を
  各ツールが直接読む、など)。中間モデル(`MasterDataModels` / `CsvSourceAttribute` 相当のもの)は
  作らない。
- **pipelineの外(`client` / `server` / `realtime_server`)への受け渡しは、必ず明示的なコピー
  スクリプトを介する**(`copy_models.py` / `copy_loader.py` / `copy_client_bytes.py` /
  `copy_realtime_bytes.py` / `copy_server_rust.py` / `copy_server_json.py`)。
- 生成とコピーは別スクリプトに分離する(生成/配置の分離)。
- 各生成・コピースクリプトは、書き込み対象ディレクトリを**処理直前に削除→再作成**してから
  書き込む(クリーンアップ。`tools/python/common.py` の `clean_dir` を使う)。これにより、
  テーブルやフィールドを削除・リネームしたときに古い生成物が残らないことを保証する。
- 生成・コピーされる全ファイルの先頭には `DO NOT EDIT` コメント(`common.py` の
  `AUTO_GENERATED_COMMENT`)を付与する。

## build.sh / run.sh の実行タイミング(予定)

ビルド(コンパイル)と実行(生成処理)は分離します。

- `build.sh`: `csharp-codegen` / `csharp_converter` / `server_codegen` をビルドする。
  **`tools/dotnet` や `tools/rust` 配下のツール本体のコードを変更したときだけ**実行すればよく、
  スキーマ(`schema/`)やスプレッドシートの中身を変更しただけのときは不要。
- `run.sh`: ダウンロード→前処理→コード生成→変換→配置までの一連の処理を実行する。
  スキーマやスプレッドシートを変更するたびに実行する。`build.sh` でビルド済みのツールを
  呼び出すだけで、ツール自体のビルドは行わない。

具体的なサブコマンドは `./run.sh` / `./build.sh`(引数無しで実行するとヘルプが出ます)を参照。

## ディレクトリ構成

```text
master-data-pipeline/
  credentials/                 # サービスアカウント鍵などの認証情報(gitignore対象)
  csv/                         # ダウンロード/配置された生CSV置き場
  out/
    normalized_csv/*.csv       # 前処理済みCSV
    generated_csharp/
      Models/*.cs
      Enums/*.cs
      MasterDataLoader.cs
    generated_rust/*.rs
    server/*.json
    masterdata.bytes           # 常に暗号化
  schema/
    enums/*.yaml                 # Enum定義(このリポジトリ単体では空。導入先で追加する)
    tables/*.yaml                 # テーブル定義(同上)
  tools/
    python/
      download_sheets.py
      normalize_csv.py
      resolve_enum_ids.py
      validate_common.py
      copy_models.py
      copy_loader.py
      copy_client_bytes.py
      copy_realtime_bytes.py
      copy_server_rust.py
      copy_server_json.py
      common.py
    dotnet/
      csharp-codegen/          # Roslynでpoco/enum/loaderを生成
      csharp_converter/        # normalized_csv + generated_csharp → masterdata.bytes
    rust/
      server_codegen/          # 既存APIサーバー向け生成(Rust)
  config.yaml
  run.sh
  build.sh
```

`client/` / `server/` / `realtime_server/` はこのリポジトリの外にある別プロジェクトで、
コピースクリプトの配置先としてのみ登場します(アプリ側の実装自体はスコープ外)。
