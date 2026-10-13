# master-data-pipeline

Google スプレッドシート(または手動配置したCSV)で入力したマスターデータを、以下の3系統に変換するパイプラインです。

- クライアント(Unity)向け: 暗号化済みバイナリ `masterdata.bytes` ([MasterMemory](https://github.com/Cysharp/MasterMemory))
- リアルタイムサーバー(MagicOnion)向け: クライアントと同一の `masterdata.bytes`
- APIサーバー(Rust、既存の別フロー)向け: `*.rs` / `*.json`

コンバーターツールは `csharp_converter` に1本化されており、クライアント/リアルタイムサーバーの区別や暗号化オプションはありません。**出力は常に暗号化されます。** 暗号化パスワードは `schema/tables/*.yaml` + `schema/enums/*.yaml` の内容から決定的に算出される `content_hash`(スキーマの指紋)です。CSVの実データは対象外なので、データを更新しただけではパスワードは変わりません。スキーマを変更した場合のみ変わり、結果として古い `masterdata.bytes` は新しい `MasterDataLoader` では復号できなくなります(想定どおりの挙動)。

## 前提ツール

- .NET SDK (net10.0)
- Rust / Cargo (edition 2024)
- Python 3.10+ と `pip install -r requirements.txt`(PyYAML, google-api-python-client, google-auth)
- GNU Make(Windowsは Git Bash 上で使う。例: `choco install make` / `scoop install make`)

コマンドは bash 上で実行します(Windowsでは Git Bash。PowerShell / cmd からは実行しない)。

## Getting Started

1. `schema/tables/*.yaml` / `schema/enums/*.yaml` でテーブル・Enumを定義する(書き方は
   [スキーマの書き方](#スキーマの書き方) を参照)。
2. データを用意する。
   - Google Sheets連携を使う場合: `config.yaml` の `google_sheets` を設定し、`make download`
     でシートの内容を `csv/*.csv` にダウンロードする。
   - 手動運用の場合: `csv/{input_csv}.csv` に直接CSVを配置する(`google_sheets` セクションは未使用でよい)。
3. `config.yaml` の `copy_destinations` を、実際に導入するプロジェクトの配置に書き換える。
   **初期状態はプレースホルダなので、書き換えずに `make generate` などの配置を含むコマンドや `copy-*` 系コマンドを実行しないこと**
   (詳細は [既知の制約・注意点](#既知の制約注意点) を参照)。
4. `make tools` でツール本体をビルドする(初回、および `tools/dotnet` / `tools/rust`
   配下のコードを変更したときのみ必要)。
5. `make check` でデータを検証する(生成物は変えない)。
6. `make generate` で、手元のCSVから全部の配置先(server / client / realtime_server)を
   作り直す。スプレッドシートから取り込んでから生成する場合は `make download generate`。
   まだ用意できていない配置先は、`config.yaml` の該当する `*_dest_dir` を `""` にしておけば
   スキップされる。

## make コマンド

生成は決まった順に流す必要があります(前処理 → 検証 → 各配置先。`realtime` は `client` の
生成物をコピーするだけなので `client` の後)。`make` はこの順番どおりに `run.sh` / `build.sh` を
呼ぶだけの入口で、処理そのものは持ちません。

| コマンド        | 流すもの                                                                 |
| --------------- | ------------------------------------------------------------------------ |
| `make check`    | `normalize-csv` → `resolve-enums` → `validate`(生成物は変えない)       |
| `make server`   | `check` → `server`                                                       |
| `make client`   | `check` → `client`                                                       |
| `make realtime` | `check` → `client` → `realtime`                                          |
| `make generate` | `check` → `server` → `client` → `realtime`。いちばんよく使う流れ         |
| `make download` | `download`(Google スプレッドシートから `csv/*.csv` に取り込むだけ)      |
| `make tools`    | `./build.sh tools`(ツール本体を変えたとき)                             |
| `make help`     | 一覧と説明(引数無しの `make` も同じ)                                   |

- 途中のコマンドが失敗した時点で止まります(検証に失敗したら生成に進まない)。
- 1回の `make` の中で同じ処理は1度しか流れません(例: `make server client` でも `check` は1回)。
- 使うpythonコマンドは環境変数 `PYTHON` で指定できます(例: `PYTHON=python3 make generate`)。
  そのまま `run.sh` へ渡ります。
- 別のリポジトリから使う場合は `make -C <このリポジトリのパス> generate` のように呼びます。

1ステップずつ流したいときは `./run.sh <command>` を直接使えます。サブコマンド一覧は
`./run.sh` / `./build.sh` を引数無しで実行すると表示されます。

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
      copy_realtime_models.py
      copy_client_loader.py
      copy_realtime_loader.py
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
  Makefile
  run.sh
  build.sh
```

`client/` / `server/` / `realtime_server/` はこのリポジトリの外にある別プロジェクトで、コピースクリプトの配置先としてのみ登場します(アプリ側の実装自体はスコープ外)。

## 全体フロー

```text
Google Sheets(1-2行目コメント、3行目型情報、4行目ヘッダー、5行目〜データ)
  ↓ download_sheets.py (手動CSV運用の場合はこのステップを使わない)
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

## スキーマの書き方

`schema/tables/*.yaml` の最小例です(以下、この `sample_item` を例に各設定項目を説明します)。

```yaml
# sample_item.yaml
name: sample_item
input_csv: sample_item_master
fields:
  - name: item_id
    type: int
    primary_key: true
  - name: name
    type: string
  - name: rarity
    type: enum
    enum_type: SampleItemRarity
  - name: shop_id
    type: int
  - name: internal_memo    # サーバーにしか出さない列の例(下記「targets」参照)
    type: string
    targets: [server]
validate:
  unique:
    - item_id
  check_relation:
    - field: shop_id
      table: sample_shop
      target_field: shop_id
```

`shop_id` が参照する `sample_shop` は、クライアントからは直接引かない参照専用テーブルの例です(下記「targets」のテーブル単位の使い方を参照)。

```yaml
# sample_shop.yaml
name: sample_shop
input_csv: sample_shop_master
targets: [server]    # クライアント向けには出力しない(DB保存・check_relation検証対象には残る)
fields:
  - name: shop_id
    type: int
    primary_key: true
  - name: name
    type: string
validate:
  unique:
    - shop_id
```

対応するEnum定義(`schema/enums/*.yaml`)はこちらです。

```yaml
# sample_item_rarity.yaml
enum: SampleItemRarity
members:
  - id: 0
    key: Common     # 生成される C#/Rust の enum メンバー名
    name: コモン    # スプレッドシート入力値(表示用)
  - id: 1
    key: Rare
    name: レア
```

- `name`: スプレッドシートのセルに入力する表示用の値(日本語可)。`resolve_enum_ids.py` が
  `name → id` に変換します。定義に無い `name` が入力された場合は検証エラーになります。
- `key`: 生成コード上のenumメンバー名。C#/Rustの識別子として妥当な値(英数字)である必要が
  あるため、`name` とは別フィールドとして持たせています。

対応するスプレッドシート/CSV(`sample_item_master`)は、次の固定レイアウトで入力します。

| 行       | 内容                                                                        |
| -------- | --------------------------------------------------------------------------- |
| 1-2行目 | 自由記述のコメント行(コンバーターは読まない)                              |
| 3行目    | 型情報行(コメント用。実際の型は `schema/tables/*.yaml` の `fields[].type` が正) |
| 4行目    | ヘッダー行(列名。`schema/tables/*.yaml` の `fields[].name` と一致させる)  |
| 5行目〜  | データ行                                                                     |

### 対応している型

`fields[].type` に指定できるのは次の5種類だけです。nullable型は無いので、値が無いことを
表現したい場合はセンチネル値で代替します(詳細は [既知の制約・注意点](#既知の制約注意点) を参照)。

- `int`(32bit。C#の`int`、Rustの`i32`、DBの`INT`に対応)
- `long`(64bit。C#の`long`、Rustの`i64`、DBの`BIGINT`に対応。intの範囲(約21億)を超える値を
  扱う場合にのみ使う)
- `string`
- `bool`(入力値は `TRUE` / `FALSE` / `True` / `False` / `1` / `0` を許容。Google Sheetsの
  チェックボックスは `TRUE` / `FALSE` を出力する)
- `enum`(`enum_type` で `schema/enums/*.yaml` の `enum` 名を指定)

### 列名の制約(Rustの予約語は使えない)

`fields[].name`(列名)は、そのまま `server_codegen` が生成する Rust struct のフィールド名として出力されます。**現状このツールは予約語のエスケープ(`r#`付与)を行わないため、Rustの予約語と一致する列名を使うとコンパイルエラーになります**(例: `type` という列名 → `pub type: ...,` は無効なRust)。C#側はPascalCase変換のおかげで実質問題になりません。

列名には以下のようなRustの予約語を避けてください(特に付けてしまいがちなもの)。迷ったら `item_type` のように接頭辞/接尾辞を付けて回避するのが簡単です。

```text
type move match loop ref use self static struct enum for in let true false
if else fn impl trait mut pub as return const
```

(完全な一覧はRust公式の [Keywords](https://doc.rust-lang.org/reference/keywords.html) を参照)

### targets

`targets` はclient(Unity/MagicOnion) / server(Rust API) のどちらに出力するかを制御するフィールドです。**省略時のデフォルトは `[client, server]`(両方)**です。フィールド単位・テーブル単位の2箇所で指定できます。

- **フィールド単位**(`fields[].targets`): サーバーにしか出さない列(内部用の説明文など)は
  `targets: [server]` のように明示します。上記の`sample_item.internal_memo`が例です。
- **テーブル単位**(トップレベル、`fields`と同じ階層): 他テーブルから参照されるだけで
  クライアントコードから直接引く必要のない参照専用テーブルに使います。`targets: [server]`
  にすると、DB保存や他テーブルからの`check_relation`検証対象には残りつつ、client向けの
  C# MemoryTableクラス(`generate-csharp`)・masterdata.bytes(`build-client`)からは
  一切出力されなくなります。上記の`sample_shop`が例です。

## 既知の制約・注意点

- **`copy_destinations` の値を空文字にするとそのコピーはスキップされる**: 配置先プロジェクトが
  まだ存在しない場合(例: realtime_server未構築)は、該当する `*_dest_dir` を `""` にしておく。
  `tools/python/common.py` の `resolve_dest_dir` が空文字を検知してコピーをスキップするため、
  存在しない祖先ディレクトリにファイルが誤生成されることはない。実プロジェクトが用意できたら
  実パスに書き換えて、`make generate`(または対応する `make` / `copy-*` コマンド)を実行する。
- **client向け・realtime_server向けのコピーは別コマンド**: `copy-models` / `copy-client-loader` /
  `copy-client-bytes` はclientのみ、`copy-realtime-models` / `copy-realtime-loader` /
  `copy-realtime-bytes` はrealtime_serverのみを対象とする(Models/Enumsも `copy_models.py` /
  `copy_realtime_models.py` に、`MasterDataLoader.cs` / `AesCrypto.cs` も
  `copy_client_loader.py` / `copy_realtime_loader.py` に分かれている)。`run.sh client` は
  client向け5コマンドのみ、`run.sh realtime` はrealtime_server向け3コマンドのみを実行する。
  `run.sh realtime` は `client` の生成物をコピーするだけなので、`make realtime` /
  `make generate` は必ず `client` の後に流す。
- **nullable型が無い**: int/long/string/bool/enumの5種類のみで、NULLを表現する型がありません。
  enum列で「値が無い」を表したい場合は `NONE` のようなセンチネルメンバー(id: 0)を定義する、
  int/long列の場合は意味のある既定値(例: 対象外を示す `0`)を割り当てる、といった運用で回避します。
- **複合主キー・複合UNIQUEが無い**: `fields[].primary_key` も `validate.unique` も単一カラムしか
  対応していません。多対多の中間テーブルなど本来複合キーが欲しいケースでは、行ごとの代理キー
  (例: `unique_id`)を1列追加して単一PKにしてください。

## 参照・コピーの原則

- **pipeline内で完結する参照は直接参照する**(例: `csharp_converter` が
  `out/generated_csharp/*.cs` を `Compile Include` で直接参照する、`schema/tables/*.yaml` を
  各ツールが直接読む、など)。中間モデル(`MasterDataModels` / `CsvSourceAttribute` 相当のもの)は
  作りません。
- **pipelineの外(`client` / `server` / `realtime_server`)への受け渡しは、必ず明示的なコピー
  スクリプトを介する**(`copy_models.py` / `copy_realtime_models.py` / `copy_client_loader.py` /
  `copy_realtime_loader.py` /
  `copy_client_bytes.py` / `copy_realtime_bytes.py` / `copy_server_rust.py` / `copy_server_json.py`)。
- 生成とコピーは別スクリプトに分離します(生成/配置の分離)。
- 各生成・コピースクリプトは、書き込み対象ディレクトリを**処理直前に削除→再作成**してから
  書き込みます(クリーンアップ。`tools/python/common.py` の `clean_dir` を使う)。これにより、
  テーブルやフィールドを削除・リネームしたときに古い生成物が残らないことを保証します。
- 生成・コピーされる全ファイルの先頭には `DO NOT EDIT` コメント(`common.py` の
  `AUTO_GENERATED_COMMENT`)を付与します。

## Makefile / build.sh / run.sh の役割分担

ビルド(コンパイル)と実行(生成処理)は分離します。`Makefile` はその上に置いた入口で、
`build.sh` / `run.sh` のコマンドを決まった順に呼ぶだけです。

- `Makefile`: よく使う流れ(`make generate` など)を1コマンドにまとめます。処理の中身は持たず、
  順序と「失敗したら止める」だけを受け持ちます。

- `build.sh`: `csharp-codegen` / `csharp_converter` / `server_codegen` をビルドします。
  **`tools/dotnet` や `tools/rust` 配下のツール本体のコードを変更したときだけ**実行すればよく、
  スキーマ(`schema/`)やスプレッドシートの中身を変更しただけのときは不要です。
  - `csharp-codegen` / `server_codegen` は生成ツールなので `dotnet build` / `cargo build` のみ。
  - `csharp_converter` は `out/generated_csharp/*.cs`(POCO)を `Compile Include` で直接参照し、
    MasterMemoryのSource Generatorがコンパイル時にテーブル/インデックスコードを生成するため、
    `run.sh build-client` 側で毎回 `dotnet run`(`--no-build`無し)を行います。
- `run.sh`: ダウンロード・前処理・コード生成・変換・配置の各処理を1つずつ実行します。
  スキーマやスプレッドシートを変更するたびに実行してください。`build.sh` でビルド済みのツールを
  呼び出すだけで、ツール自体のビルドは行いません。
