# Contributing

## Editing sample assets

`Samples~` ends in a tilde, so Unity hides it from the Asset Database: scenes, prefabs and
materials inside it cannot be opened or created while the folder is named that way.

To edit them:

1. Rename `Samples~` to `Samples` and let Unity import it.
2. Edit the scene or assets normally.
3. Save, then rename the folder back to `Samples~`.
4. Delete the generated `Samples.meta` — the folder itself is not an asset any more.

**Keep every `.meta` file inside `Samples~`.** Package Manager copies the sample folder verbatim on
import; the scene references its material and texture by GUID, and those GUIDs only survive if the
`.meta` files travel with them. Deleting them ships a sample whose scene has missing references.

Do the rename with the editor open; Unity picks it up on the next refresh.

## Sample folder names

Every folder under `Samples~` must match a `samples[].path` entry in `package.json`.
`SamplesLayoutTests` fails the build if they drift.
