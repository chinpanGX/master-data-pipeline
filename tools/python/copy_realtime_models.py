"""out/generated_csharp/{Models,Enums}/*.cs を realtime_server 側へ配置する(clientと同一内容)。

Models/ Enums/ はこのスクリプトが専有する生成物ディレクトリなので、
コピー前にそれぞれクリーンアップする(古いクラスの残留を防ぐ)。
MasterDataLoader.cs / AesCrypto.cs は copy_realtime_loader.py が別途配置する
(MemoryDatabase等はこのModels/Enumsから配置先のSource Generatorが生成するため、
realtime_serverのビルドには両方が揃っている必要がある)。
"""

from __future__ import annotations

import sys

from common import REPO_ROOT, copy_dir_contents, load_config, resolve_dest_dir


def main() -> None:
    config = load_config()
    generated_dir = REPO_ROOT / config["csharp_codegen"]["output_dir"]
    dest = config["copy_destinations"]

    models_src = generated_dir / "Models"
    enums_src = generated_dir / "Enums"

    if not models_src.exists() or not enums_src.exists():
        raise FileNotFoundError(
            f"{generated_dir} が見つかりません(先にcsharp-codegenを実行してください)"
        )

    models_dest = resolve_dest_dir(dest, "realtime_models_dest_dir")
    if models_dest is None:
        print("skip: realtime_models_dest_dir が未設定のためコピーをスキップしました")
    else:
        copied = copy_dir_contents(models_src, models_dest, "*.cs")
        print(f"copied {len(copied)} files: {models_src} -> {models_dest}")

    enums_dest = resolve_dest_dir(dest, "realtime_enums_dest_dir")
    if enums_dest is None:
        print("skip: realtime_enums_dest_dir が未設定のためコピーをスキップしました")
    else:
        copied = copy_dir_contents(enums_src, enums_dest, "*.cs")
        print(f"copied {len(copied)} files: {enums_src} -> {enums_dest}")


if __name__ == "__main__":
    try:
        main()
    except FileNotFoundError as e:
        print(f"エラー: {e}", file=sys.stderr)
        sys.exit(1)
