# CLAUDE.md

master-data-pipeline のメンテナンス用ガイド。仕様の詳細は `README.md` が正。ここには「作業するときに守ること」だけを書く。

## このプロジェクトは何か

`schema/tables/*.yaml` と `schema/enums/*.yaml` でマスターデータの型を定義し、CSV(Google Sheets経由または手動配置)から次を自動生成するツール群。

- クライアント(Unity) / リアルタイムサーバー(MagicOnion)向け: C# POCO・Enum・`MasterDataLoader.cs`・暗号化済み `masterdata.bytes`(MasterMemory)
- APIサーバー(Rust)向け: `*.rs` / `*.json`

アプリ本体(`client/` `server/` `realtime_server/`)はこのリポジトリの外。ここでは「コピー先」としてのみ登場し、スコープ外。

## 構成と役割

| 場所 | 役割 |
| --- | --- |
| `tools/python/` | 前処理(download / normalize / resolve-enums / validate)と、外部プロジェクトへのコピー(`copy_*.py`)、共通処理(`common.py`) |
| `tools/dotnet/csharp-codegen/` | schema → C# POCO / Enum / Loader / AesCrypto / content_hash(Roslyn)。`SchemaLoader` などは converter からも参照される |
| `tools/dotnet/csharp_converter/` | normalized_csv + 生成C# → `masterdata.bytes`(MasterMemory + CsvHelper、リフレクションで型解決) |
| `tools/rust/server_codegen/` | schema + CSV → `out/generated_rust/*.rs`, `out/server/*.json` |
| `Makefile` / `run.sh` / `build.sh` | 実行順序の入口 / 生成処理 / ツール本体のビルド |
| `config.yaml` | プロジェクト固有の設定(**gitignore対象**。コミットしない) |

## よく使うコマンド(bash / Windowsは Git Bash で実行)

- `make tools` : ツール本体のビルド。`tools/dotnet` `tools/rust` を変更したときだけ必要
- `make check` : normalize → resolve-enums → validate(生成物は変えない)
- `make generate` : check → server → client → realtime(通常の全体生成)
- 個別実行は `./run.sh <command>`(引数なしで一覧)
- python は環境変数 `PYTHON` で切り替え可(例: `PYTHON=python3 make generate`)

## 守るべき設計ルール(壊しやすいところ)

1. **生成物は直接編集しない。** `out/` は gitignore 対象。生成ファイルの先頭には `common.py` の `AUTO_GENERATED_COMMENT`(DO NOT EDIT)を付ける。
2. **参照とコピーの原則。** pipeline内で完結する参照は直接参照し、中間モデルを増やさない。pipeline外(client / server / realtime_server)への受け渡しは必ず `copy_*.py` を介す。**生成とコピーは別スクリプトに分ける。**
3. **クリーンアップ。** 生成・コピー系は書き込み先を処理直前に `clean_dir` で削除→再作成する(古い生成物を残さないため)。ただしUnity側のコピー先は `copy_dir_contents(preserve_meta=True)` で `.meta` とGUIDを保持する。この挙動を壊さない。
4. **`config.yaml` の `copy_destinations` が空文字 `""` ならそのコピーはスキップ**(`resolve_dest_dir` が `None` を返す)。空文字をリポジトリルートに解決する実装にしない。
5. **`run.sh build-client` は `--no-build` を付けない。** MasterMemory の Source Generator が生成C#から毎回コードを作るため。`generate-csharp` / `build-server` はビルド済み前提で `--no-build` 相当。
6. **`csharp_converter.csproj` の `RootNamespace` は `config.yaml` の `csharp_codegen.namespace` と手動で一致させる。** 変更時は両方、およびUnity側 asmdef・realtime_server側プロジェクトの整合も確認する。
7. **暗号化は常時有効。** パスワードは schema(tables + enums)由来の `content_hash`。CSVデータでは変わらず、スキーマ変更で旧 `masterdata.bytes` は復号不能になる(仕様)。この仕様を変える場合はREADMEの冒頭説明も更新する。
8. **対応型は `int` `long` `string` `bool` `enum` の5種類のみ。** nullable・複合主キー・複合UNIQUEは未対応(README「既知の制約」参照)。`int`=C# int/Rust i32/DB INT、`long`=C# long/Rust i64/DB BIGINT の幅を揃えること。
9. **列名はRustの予約語を避ける**(`r#` エスケープ未対応)。`validate` での検出などは追加の価値あり。
10. **`targets`(省略時 `[client, server]`)** はフィールド単位・テーブル単位の両方で効く。新しい出力先や生成物を足すときは、client向け(C#/bytes)とserver向け(Rust/JSON)の両方で `targets` を尊重すること。

## 変更時のチェックリスト

### スキーマの項目や型を追加・変更するとき
関係する箇所を漏れなく更新する。

- `tools/dotnet/csharp-codegen/`: `Models/*Definition.cs`、`SchemaLoader.cs`、`Generators/*`(POCO / Enum / Loader / `ContentHashCalculator`)
- `tools/dotnet/csharp_converter/Program.cs`(CSV → 行オブジェクトの変換)
- `tools/rust/server_codegen/src/`: `schema.rs` `converter.rs` `generator.rs`
- `tools/python/`: `normalize_csv.py` `resolve_enum_ids.py` `validate_common.py`
- `README.md`(スキーマの書き方、対応型、既知の制約)

C#側とRust側で型の意味・幅・変換結果(bool表記、enumのid解決など)が食い違わないことを確認する。

### 出力先や配置スクリプトを足すとき
- `config.yaml` の `copy_destinations` に項目を追加(空文字スキップ対応を必ず入れる)
- `run.sh` にコマンドとまとめコマンド、`Makefile` に順序(依存関係)を追加。**並列実行禁止(`.NOTPARALLEL`)の前提を崩さない**
- README の make コマンド表・ディレクトリ構成・全体フローを更新

### 完了前の確認(可能な範囲で)
1. `make tools` でビルドが通る
2. サンプルの schema + CSV(`sample_item` 等、README参照)を一時的に置いて `make check` → 生成 → 出力を目視確認。**確認後は `schema/` `csv/` に置いたサンプルを必ず片付ける**(このリポジトリ単体では空が正)
3. 生成物のコンパイル確認(C#は `csharp_converter` がビルドできること、Rustは生成された `.rs` が構文として妥当なこと)
4. 仕様を変えたら README を同じ変更の中で更新する

## 触る前に確認が必要なもの

- `config.yaml` の `copy_destinations` は導入先プロジェクトの実パスを指す。**実パスが入っている状態で `copy-*` / `make generate` を実行すると、外部プロジェクトの `Models/` 等を削除→再作成する。** 実行前に配置先をユーザーに確認する。
- `credentials/` のサービスアカウント鍵は読まない・出力しない・コミットしない。
- `git push`・履歴の書き換えなどはユーザーの明示的な指示があるときだけ。

## 開発スタイル

- 回答・コメント・コミットメッセージ・READMEは日本語。コミットは1論点1コミットで、既存の履歴のように「何をどうしたか/なぜ」を簡潔に書く。
- コードコメントは「なぜそうしているか」を書く(既存コードの方針に合わせる)。
- スクリプトは Windows(cp932コンソール)でも動くこと。Pythonの出力は UTF-8 前提(`common.py` で reconfigure 済み)。Makefileのレシピに日本語を直接 `echo` しない。
- ユーザーはUnity/C#が本業でRustは学習中。Rust側(`tools/rust/server_codegen`)を変更したときは、所有権・Result/`?`・イテレータなどのRustらしい書き方を使った箇所に、C#との対比で一言説明を添える。
- 新しい依存(NuGet / crate / pip)を足す前に、既存の依存で済まないか確認し、足す場合は理由を伝える。
