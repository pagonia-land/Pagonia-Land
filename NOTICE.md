# Notice

This repository is an unofficial research and documentation project for understanding the structure of the Pioneers of Pagonia game database.

Pioneers of Pagonia is a trademark of [Envision Entertainment GmbH](https://www.envision-entertainment.de/).

## Game Data

The extracted game database files are not part of this repository's license.

The local `game-gdb/` directory is ignored by Git and is intended only for locally extracted files from a user's own game installation. These files remain the property of their respective rights holders.

Do not publish the extracted `game-gdb/` directory in this repository unless you have permission from the rights holder.

## Generated Data

The generated files under `generated/` are derived from local extracted game data and are also ignored by Git by default.

They can be regenerated locally with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\analyze_database.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

## Licenses

Original scripts and tooling are licensed under the MIT License. See [LICENSE](LICENSE).

Original documentation is licensed under CC BY 4.0. See [LICENSE-DOCS.md](LICENSE-DOCS.md).

## Project Credits

[Pioneers of Pagonia](https://pioneersofpagonia.com/) is a trademark of [Envision Entertainment GmbH](https://www.envision-entertainment.de/).

Copyright (c) 2026 - [Pagonia Land](https://pagonia.land/) and [contributors](https://github.com/pagonia-land/Pagonia-Land/graphs/contributors).

Modding community project by [Lava Block](https://lava-block.com/).

Documentation and repository structure were created with assistance from [OpenAI Codex](https://openai.com/codex/) (initial scaffolding) and [Anthropic Claude](https://www.anthropic.com/claude) (ongoing development).
