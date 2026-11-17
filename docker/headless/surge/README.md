# Nota headless + Surge XT

Image légère par-dessus `nota-headless:latest` : installe Surge XT 1.3.4 (VST3, GPL-3.0) et remplace
`Nota.Headless.dll` (outils `scan_plugins`, `surge_patches`, `load_surge_patch`) sans recompiler le moteur.

```sh
dotnet build src/managed/Nota.Headless -c Release -o out && cp out/Nota.Headless.dll docker/headless/surge/
docker build -t nota-headless:surge docker/headless/surge
```
Après démarrage : `scan_plugins` (une fois), puis `add_plugin_instrument` + `load_surge_patch`.
