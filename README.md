# TaskFlow - Clean Architecture API

## Overview

TaskFlow is a Task Management and Attendance Tracking API built with ASP.NET Core to demonstrate modern backend architecture and software engineering practices.

The project showcases:

* Clean Architecture
* Domain-Driven Design (DDD)
* CQRS (Command Query Responsibility Segregation)
* Domain Events
* Repository Pattern (Write Side)
* Dapper (Read Side)
* JWT Authentication & Authorization
* Fluent Validation
* Custom Middleware
* Global Exception Handling
* Structured Logging

The primary goal of this project is to demonstrate how enterprise-grade ASP.NET Core applications can be designed for maintainability, scalability, testability, and separation of concerns.

---

# Architecture Overview

The solution follows Clean Architecture principles.

```text
TaskFlow.Api
      ↓
TaskFlow.Application
      ↓
TaskFlow.Domain

TaskFlow.Infrastructure
      ↑
Implements Application Contracts
```

Dependency Rule:

* Domain depends on nothing.
* Application depends only on Domain.
* Infrastructure depends on Application and Domain.
* API depends on Application and Infrastructure.

This ensures business rules remain independent from frameworks, databases, and external services.

---

# Domain-Driven Design (DDD)

The Domain layer contains the core business rules of the application.

## Goals

The Domain layer should answer:

"What does the business do?"

instead of:

"How is data stored?"

or

"How is data retrieved?"

---

## Entities

Entities represent business objects with identity.

Example:

```text
User
Role
Task
Project
```

Characteristics:

* Have unique identity.
* Contain business behavior.
* Protect their own state.
* Raise domain events when important actions occur.

Example:

```text
User.Register()
User.VerifyEmail()
User.ChangePassword()
```

Business logic is kept inside entities rather than controllers.

---

## Aggregate Roots

Aggregate Roots protect consistency boundaries.

Example:

```text
User : IAggregateRoot
```

Benefits:

* Prevent invalid state changes.
* Centralize business operations.
* Enforce invariants.

---

## Value Objects

Value Objects model concepts without identity.

Examples:

```text
Email
PhoneNumber
FullName
```

Benefits:

* Validation occurs once.
* Strong typing.
* Eliminates primitive obsession.

Instead of:

```text
string Email
```

Use:

```text
Email Email
```

This guarantees every email in the system is valid.

---

## Domain Events

Domain Events represent business events that occurred inside the domain.

Examples:

```text
UserRegisteredEvent
UserEmailVerifiedEvent
UserPasswordChangedEvent
```

Benefits:

* Decouple workflows.
* Improve maintainability.
* Allow new features without modifying existing domain logic.

Example Flow:

```text
User.Register()
        ↓
UserRegisteredEvent
        ↓
Event Handler
        ↓
Send Welcome Email
```

The User entity does not know anything about email services.

---

# CQRS

The project follows CQRS using MediatR.

Commands modify data.

Examples:

```text
RegisterUserCommand
CreateTaskCommand
UpdateTaskCommand
```

Queries retrieve data.

Examples:

```text
GetUserByIdQuery
GetTasksQuery
AttendanceReportQuery
```

Benefits:

* Clear separation of responsibilities.
* Better scalability.
* Easier maintenance.
* Optimized read and write paths.

---

# Repository Pattern (Write Side)

Repositories are used only for write operations.

Examples:

```text
IUserRepository
ITaskRepository
```

Responsibilities:

* Persist aggregates.
* Encapsulate EF Core logic.
* Keep Application Layer independent from EF Core.

Why only Write Side?

Commands focus on business consistency and aggregate management.

---

# Dapper (Read Side)

Dapper is used for query operations.

Examples:

```text
GetUserProfileQuery
GetTaskListQuery
AttendanceReportQuery
```

Benefits:

* Faster read performance.
* Optimized SQL execution.
* Lightweight object mapping.
* Better reporting queries.

Architecture:

```text
Command → Repository → EF Core

Query → Dapper → SQL → DTO
```

---

# Validation Pipeline

The project uses FluentValidation with MediatR Pipeline Behaviors.

Flow:

```text
Request
   ↓
ValidationBehavior
   ↓
Handler
```

Benefits:

* Centralized validation.
* Cleaner handlers.
* Consistent validation process.

---

# Authentication & Authorization

Authentication is implemented using JWT Bearer Tokens.

Features:

* User Login
* Token Generation
* Protected Endpoints
* Role-Based Authorization

Flow:

```text
Login
   ↓
JWT Token
   ↓
API Request
   ↓
Authentication Middleware
   ↓
Authorization Policy
```

---

# Custom Middleware

## Global Exception Middleware

Responsibilities:

* Catch unhandled exceptions.
* Return standardized API responses.
* Prevent application crashes.

Benefits:

* Consistent error responses.
* Centralized exception handling.

---

## Request Logging Middleware

Responsibilities:

* Log incoming requests.
* Log outgoing responses.
* Measure execution time.

Benefits:

* Easier debugging.
* Better observability.
* Request tracing.

---

# Project Structure

```text
TaskFlow.Api
│
├── Controllers
├── Middleware
├── Extensions

TaskFlow.Application
│
├── Commands
├── Queries
├── Validators
├── Behaviors
├── DomainEventHandlers

TaskFlow.Domain
│
├── Entities
├── ValueObjects
├── DomainEvents
├── Enums

TaskFlow.Infrastructure
│
├── Persistence
├── Repositories
├── Authentication
├── Dapper
├── Services
```

---

# Key Learning Outcomes

This project demonstrates:

* Designing domain-centric applications.
* Applying DDD tactical patterns.
* Implementing CQRS with MediatR.
* Using Domain Events for decoupled workflows.
* Separating read and write models.
* Building maintainable APIs using Clean Architecture.
* Implementing authentication and authorization.
* Creating reusable middleware components.

---

# Future Enhancements

Planned improvements:

* Refresh Tokens
* Audit Trail
* Caching with Redis
* Background Jobs using Hangfire
* Event Bus Integration
* Unit Testing
* Integration Testing
* Docker Support
* CI/CD Pipeline
* Multi-Tenant Architecture
