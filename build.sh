#!/usr/bin/env bash
# ツール本体のビルド(コンパイル)専用スクリプト。生成処理の実行は run.sh が行う。
# tools/dotnet, tools/rust 配下のツールのコードを変更したときだけ実行すればよく、
# schema/ やスプレッドシートの中身を変更しただけのときは不要。
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

usage() {
    cat <<'EOF'
Usage: ./build.sh <command>

Commands:
  tools   csharp-codegen / csharp_converter / server_codegen をビルドする
EOF
}

build_dotnet() {
    local name="$1"
    local dir="tools/dotnet/$name"
    if [ ! -f "$dir/$name.csproj" ]; then
        echo "skip: $dir/$name.csproj が見つかりません(未実装)"
        return 0
    fi
    echo "==> dotnet build $dir"
    dotnet build "$dir"
}

build_rust() {
    local dir="tools/rust/server_codegen"
    if [ ! -f "$dir/Cargo.toml" ]; then
        echo "skip: $dir/Cargo.toml が見つかりません(未実装)"
        return 0
    fi
    echo "==> cargo build --manifest-path $dir/Cargo.toml"
    cargo build --manifest-path "$dir/Cargo.toml"
}

cmd_tools() {
    build_dotnet csharp-codegen
    build_dotnet csharp_converter
    build_rust
}

case "${1:-}" in
    tools)
        cmd_tools
        ;;
    *)
        usage
        exit 1
        ;;
esac
