#!/usr/bin/env bash
# 生成処理の実行専用スクリプト。ツール本体のビルドは build.sh が行う
# (build.sh でビルド済みであることが前提。ビルドされていないツールを呼ぶ
# コマンドは、その場でエラーになる)。
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

PYTHON="${PYTHON:-python}"

usage() {
    cat <<'EOF'
Usage: ./run.sh <command>

前処理:
  download              Google スプレッドシート -> csv/*.csv
  normalize-csv          csv/*.csv -> out/normalized_csv/*.csv (先頭3行除去)
  resolve-enums           out/normalized_csv/*.csv の enum列を name -> id に変換
  validate                 out/normalized_csv/*.csv を検証(unique / check_relation)

クライアント(Unity / MagicOnion)向け:
  generate-csharp          schema -> out/generated_csharp/*.cs (csharp-codegen)
  build-client              out/normalized_csv/*.csv -> out/masterdata.bytes (csharp_converter)
  copy-models               out/generated_csharp/{Models,Enums} -> client
  copy-loader                out/generated_csharp/MasterDataLoader.cs -> client/realtime_server
  copy-client-bytes          out/masterdata.bytes -> client
  copy-realtime-bytes        out/masterdata.bytes -> realtime_server
  client                    上記6コマンドをまとめて実行

サーバー(Rust API、既存)向け:
  build-server              schema -> out/generated_rust/*.rs, out/server/*.json (server_codegen)
  copy-server-rust           out/generated_rust/*.rs -> server
  copy-server-json           out/server/*.json -> server
  server                    上記3コマンドをまとめて実行

全体:
  all                       download から client / server まで一気通貫で実行

環境変数 PYTHON で使用するpythonコマンドを指定できます(未指定なら "python")。
EOF
}

run_py() {
    "$PYTHON" "tools/python/$1"
}

cmd_download()             { run_py download_sheets.py; }
cmd_normalize_csv()        { run_py normalize_csv.py; }
cmd_resolve_enums()        { run_py resolve_enum_ids.py; }
cmd_validate()              { run_py validate_common.py; }

cmd_generate_csharp() {
    dotnet run --no-build --project tools/dotnet/csharp-codegen
}

cmd_build_client() {
    # csharp_converter は out/generated_csharp/*.cs を Compile Include しており、
    # MasterMemory の Source Generator がその内容(POCO)からコンパイル時に
    # DatabaseBuilder/MemoryDatabase を生成する。つまり generate-csharp のたびに
    # 再コンパイルが必要なため、ここだけは --no-build を付けない
    # (schema変更を反映しないまま古いビルドで実行され、失敗または古い内容で
    # masterdata.bytesが作られてしまうため)。
    dotnet run --project tools/dotnet/csharp_converter
}

cmd_copy_models()          { run_py copy_models.py; }
cmd_copy_loader()           { run_py copy_loader.py; }
cmd_copy_client_bytes()     { run_py copy_client_bytes.py; }
cmd_copy_realtime_bytes()   { run_py copy_realtime_bytes.py; }

cmd_client() {
    cmd_generate_csharp
    cmd_build_client
    cmd_copy_models
    cmd_copy_loader
    cmd_copy_client_bytes
    cmd_copy_realtime_bytes
}

cmd_build_server() {
    cargo run --quiet --manifest-path tools/rust/server_codegen/Cargo.toml
}

cmd_copy_server_rust()      { run_py copy_server_rust.py; }
cmd_copy_server_json()      { run_py copy_server_json.py; }

cmd_server() {
    cmd_build_server
    cmd_copy_server_rust
    cmd_copy_server_json
}

cmd_all() {
    cmd_download
    cmd_normalize_csv
    cmd_resolve_enums
    cmd_validate
    cmd_client
    cmd_server
}

case "${1:-}" in
    download) cmd_download ;;
    normalize-csv) cmd_normalize_csv ;;
    resolve-enums) cmd_resolve_enums ;;
    validate) cmd_validate ;;
    generate-csharp) cmd_generate_csharp ;;
    build-client) cmd_build_client ;;
    copy-models) cmd_copy_models ;;
    copy-loader) cmd_copy_loader ;;
    copy-client-bytes) cmd_copy_client_bytes ;;
    copy-realtime-bytes) cmd_copy_realtime_bytes ;;
    client) cmd_client ;;
    build-server) cmd_build_server ;;
    copy-server-rust) cmd_copy_server_rust ;;
    copy-server-json) cmd_copy_server_json ;;
    server) cmd_server ;;
    all) cmd_all ;;
    *)
        usage
        exit 1
        ;;
esac
