# Object-Oriented Programming in BHL

## Classes

### Class Definition

Classes in BHL are defined using the `class` keyword. When accessing class members or methods within a class, you must use the `this` keyword:

```bhl
class Person {
    string name
    int age
    
    func void SayHello() {
        trace("Hello, " + this.name)
    }
    
    func void SetDetails(string name, int age) {
        this.name = name  // Must use this to access fields
        this.age = age
    }
    
    func string GetGreeting() {
        return this.FormatGreeting()  // Must use this for method calls too
    }
    
    func string FormatGreeting() {
        return "Hello, " + this.name + ", age " + (string)this.age
    }
}
```

Important rules about `this`:
1. Required when accessing any instance field within the class
2. Required when calling any instance method within the class
3. Not required when accessing parameters or local variables
4. Not required when accessing static members

### Creating Objects

Objects can be created and initialized in two ways:

1. Using separate assignments:
```bhl
var person = new Person
person.name = "John"
person.age = 30
```

2. Using JSON-like initialization syntax:
```bhl
// Basic object initialization
var person = new Person {
    name = "John",
    age = 30
}

// Nested objects
var employee = new Employee {
    name = "John",
    age = 30,
    department = "Engineering",
    address = new Address {
        street = "123 Main St",
        city = "San Francisco"
    }
}

// Arrays in initialization
var team = new Team {
    name = "Engineering",
    members = [
        new Person { name = "John", age = 30 },
        new Person { name = "Alice", age = 25 }
    ],
    skills = ["C#", "BHL", "Git"]
}

// Maps in initialization
var company = new Company {
    name = "TechCorp",
    departments = {
        "eng": new Department { name = "Engineering", size = 50 },
        "hr": new Department { name = "HR", size = 10 }
    },
    config = {
        "max_employees": 100,
        "location": "SF"
    }
}

// Mixed arrays, maps, and objects
var project = new Project {
    name = "BHL 2.0",
    team = [
        new Developer {
            name = "John",
            skills = ["BHL", "C#"],
            metadata = {
                "level": "senior",
                "start_date": "2024"
            }
        },
        new Developer {
            name = "Alice",
            skills = ["Python", "BHL"],
            metadata = {
                "level": "mid",
                "start_date": "2025"
            }
        }
    ]
}

## Inheritance

BHL supports single inheritance using the `:` syntax:

```bhl
class Employee : Person {
    string department
    
    func void Work() {
        // Method implementation
    }
}
```

### Important Inheritance Rules:
- Self-inheritance is not allowed
- A class can only inherit from one base class
- The `base` keyword can be used to access parent class members

### Base Class Access

In derived classes, you can use the `base` keyword to access members of the base class:

```bhl
class Base {
    int a
    func int getA() {
        return this.a
    }
}

class Derived : Base {
    int new_a
    
    func int getTotal() {
        // Access base class field
        return base.a + this.new_a
    }
    
    // Override base method
    override func int getA() {
        // Call base class method
        return base.getA() + this.new_a
    }
}
```

Important notes about `base`:
1. The `base` keyword can only be used in classes that inherit from another class
2. It provides access to both fields and methods of the base class
3. Commonly used in overridden methods to call the base class implementation
4. Cannot be used in a root class (class without inheritance)

### Virtual and Override Methods

BHL supports method overriding through the `virtual` and `override` keywords:

```bhl
class Base {
    virtual func int calculate() {
        return 42
    }
}

class Derived : Base {
    // Must use 'override' to override a virtual method
    override func int calculate() {
        return 100
    }
}
```

Important rules for virtual methods:
1. Only methods marked as `virtual` can be overridden
2. Overriding methods must use the `override` keyword
3. Method signatures must match exactly (including return type and parameters)
4. Virtual methods cannot have default arguments
5. Both regular and coroutine methods can be virtual

Example with base class call:
```bhl
class Foo {
    int a
    
    virtual func int getA() {
        return this.a
    }
}

class Bar : Foo {
    int new_a
    
    override func int getA() {
        // Can call base class implementation
        return base.getA() + this.new_a
    }
}
```

Virtual coroutines example:
```bhl
class Base {
    coro virtual func int process() {
        yield()
        return 42
    }
}

class Derived : Base {
    coro override func int process() {
        yield()
        return 100
    }
}
```

## Class Members

### Fields
- Instance fields
- Static fields
- Access modifiers (if applicable)

### Methods
```bhl
class Calculator {
    // Instance method
    func int Add(int a, int b) {
        return a + b
    }
    
    // Static method
    static func int Multiply(int a, int b) {
        return a * b
    }
}
```

### Properties
Fields can be accessed directly in BHL:
```bhl
class User {
    string name
    int age
}

func test() {
    var user = new User
    user.name = "Alice"  // Direct field access
    user.age = 25
}
```

## Interfaces

### Interface Definition
```bhl
interface IMovable {
    func void Move(float x, float y)
    func float GetSpeed()
}
```

### Implementing Interfaces
```bhl
class Car : IMovable {
    func void Move(float x, float y) {
        // Implementation
    }
    
    func float GetSpeed() {
        // Implementation
        return 0.0
    }
}
```
