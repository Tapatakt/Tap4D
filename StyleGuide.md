# Tap4D Style Guide

## General

- Target: .NET 10, C# (latest language version), nullable reference types enabled.
- GPU shaders: ComputeSharp.

## Comments

- XML doc comments **in English** on:
  - all methods, including private ones;
  - all non-private fields and properties.
- No comments that merely restate what the code does.

## Braces

- Omit optional braces for single-statement `if` / `else` / `for` / `foreach` / `while` bodies,
  including nested single-statement bodies (e.g. an `if` whose whole body is one `for`).

## Type names and `var`

- Do not use `var`, except when the type is a generic with a long name.
- Omit redundant type names otherwise (target-typed `new()`).
- Use primary constructors when possible.

```csharp
// Correct:
SomeClass a = new();
Dictionary<string, List<int>> map = new(); // long generic name: var also acceptable

// Wrong:
var a = new SomeClass();
```

## Size limits

- Methods must fit on one screen: ~30 lines max, preferably less.
- Classes should follow the single-responsibility principle; over ~200 lines is a smell.
- Strictly one class per file (except tiny helper classes that exist only to serve another class).

## Collections

- Use collection expressions (`[1, 2, 3]`, `[]`) when they make code shorter.
