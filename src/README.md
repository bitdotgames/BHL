# BHL Runtime (Unity)

[BHL](https://github.com/bitdotgames/bhl) is a strictly typed programming language
tailored for gameplay logic scripting — pseudo-parallel execution primitives with a
familiar imperative coding style. This package embeds the BHL runtime (and an optional
in-editor compiler) into Unity.

## Requirements

- Unity **2021.3** or newer
- `com.unity.nuget.newtonsoft-json` (installed automatically as a dependency)

## Installation

### OpenUPM

```
openupm add com.bitgames.bhl
```

Or add a scoped registry in **Project Settings → Package Manager**:

- Name: `OpenUPM`
- URL: `https://package.openupm.com`
- Scope(s): `com.bitgames`

then install **BHL Runtime** from **My Registries** in the Package Manager window.

### Git URL

**Package Manager → Add package from git URL…**

```
https://github.com/bitdotgames/bhl.git?path=/src#unity-v3.0.0
```

## Runtime vs. compiler

By default this package ships the **runtime only** — enough to execute precompiled
`.bhc` bytecode. The compiler front-end is guarded by the `BHL_PARSER` / `BHL_FRONT`
defines and is compiled out unless the ANTLR plugin is present. Enabling in-editor
compilation of `.bhl` sources is opt-in; see the
[main repository](https://github.com/bitdotgames/bhl) for setup.

## Documentation

Language documentation and examples live in the
[BHL repository](https://github.com/bitdotgames/bhl).

## License

MIT — see [LICENSE](https://github.com/bitdotgames/bhl/blob/master/LICENSE).
