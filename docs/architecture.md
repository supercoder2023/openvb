# Architecture and project format

## Decisions

- The solution targets .NET 10. The IDE is a Windows-only WinForms executable; the core and its tests remain plain `net10.0` projects so language and persistence code can be tested independently.
- `OpenVBStudio.Core` has no UI dependency. Its project model is persisted through `ProjectService`; its language path is divided into `BasicLexer`, `BasicParser`, `BasicSemanticAnalyzer`, and `BasicInterpreter`.
- The initial designer represents controls and nested menu items as data (`ControlDocument`, `MenuItemDocument`) and renders those records in the form designer and runtime. This keeps JSON persistence independent of WinForms and allows future designer/runtime backends.
- Runtime event handlers provide the BASIC interpreter a form-display callback; `FormName.Show` resolves the name against the current project's forms and displays that runtime form.
- The project format is JSON to make the first iteration inspectable and easy to extend. Legacy `.vbp`, `.frm`, `.bas`, and `.cls` support should be implemented as import/export adapters around this model, not added to the interpreter or designer.
- The interpreter is intentionally a small VB-like subset. It is not a compiler and does not claim VB6 compatibility or generate standalone executables.
- `ICompilationBackend` defines the future build boundary and returns structured diagnostics. No compilation backend is provided yet.

## JSON project shape

The serializer writes indented JSON with camel-case property names. For example:

```json
{
  "name": "Project1",
  "forms": [
    {
      "name": "Form1",
      "caption": "Form1",
      "width": 640,
      "height": 480,
      "controls": [
        {
          "name": "Command1",
          "kind": "CommandButton",
          "caption": "Command1",
          "left": 24,
          "top": 24,
          "width": 100,
          "height": 28,
          "enabled": true,
          "visible": true
        }
      ],
      "code": ""
    }
  ]
}
```

Form source is stored inline in the `code` property. Control positions and dimensions are integer client-area pixels. Project validation rejects empty form lists, invalid form dimensions, and duplicate control names within a form.

## Future compiler boundary

The interpreter is deliberately isolated from the WinForms project, making it possible to introduce a compilation backend behind a future execution/build interface. Native code generation, .NET executable generation, debugger integration, and legacy VB6 source import are not included in this initial prototype.
