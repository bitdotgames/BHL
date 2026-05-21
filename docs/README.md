# BHL Language Documentation

BHL is a strongly-typed programming language implemented in .NET, designed for game development scenarios. It's a language that provides a balance between simplicity and power, making it ideal for gameplay logic scripting. Think of it as of glue between C# and gameplay logic. Currently it's in beta state and targets only C# platform (while it's planned to target other platforms like C++). Nonetheless it has been battle tested in the real world projects and heavily used by BIT.GAMES for mobile games development built with [Unity](https://unity.com/).

This documentation provides a comprehensive guide to the language features and usage.

## Table of Contents

1. [Getting Started](getting-started.md)
   - Language Overview
   - Installation
   - Your First BHL Program

2. [Core Language Features](core-features.md)
   - Basic Types
   - Variables and Constants
   - Operators
   - Control Flow
   - Enums

3. [Functions](functions.md)
   - Basic Functions
   - Function Pointers
   - Static Functions
   - Lambda Functions
   - Coroutines

4. [Pseudo Parallel Execution](pseudo-parallel.md)
    - Basic Parallel Blocks
    - Control Flow
    - Nested Execution

5. [Object-Oriented Programming](oop.md)
   - Classes
   - Inheritance
   - Interfaces
   - Members and Methods

6. [Type Casting](type-casting.md)
   - Basic Type Casting
   - String Conversions
   - Class Type Casting
   - Safe Casting
   - Collection Casting
   - The 'any' Type

7. [Collections](collections.md)
   - Arrays
   - Collection Operations
   - Memory Management

8. [Maps](maps.md)
   - Map Declaration
   - Map Operations
   - Iterating Maps
   - Memory Management

9. [Namespaces](namespaces.md)
    - Namespace Declaration
    - Nested Namespaces
    - Importing Namespaces
    - Namespace Resolution
    - Type Safety

10. [Defer Statement](defer.md)
    - Basic Usage
    - Key Features
    - Best Practices

11. [Imports](imports.md)
    - Module System
    - Import Rules
    - Best Practices

12. [Operator Overloading](operator-overloading.md)
    - Supported Operators
    - Implementation Rules
    - Common Patterns

13. [Yield](yield.md)
    - Coroutine Basics
    - Yield Variants
    - Common Patterns

14. [Fibers](fibers.md)
    - Basic Concepts
    - Fiber Hierarchy
    - Function Pointers
    - Advanced Features
    - Best Practices

15. [Standard Library](standard-library.md)
    - Core Module (std)
    - IO Module (std/io)
    - Custom Modules

16. [C# Bindings](csharp-bindings.md)
    - Native Class Bindings
    - Fields and Methods
    - Error Handling
    - Best Practices

17. [LSP — IDE Integration](lsp.md)
    - VS Code setup
    - Sublime Text setup
    - Neovim setup
    - Language features

18. [DAP — Debugger](dap.md)
    - Architecture
    - Unity integration
    - VS Code client

## Contributing

Contributions are welcome! You can contribute to BHL by:
- Opening issues on GitHub to report bugs or suggest features
- Submitting pull requests with improvements

## License

BHL is licensed under the MIT License. 
