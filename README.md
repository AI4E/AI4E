# AI4E
The development of modern enterprise applications imposes a lot of work on the developers for handling infrastructural needs, like modularization, module hosting and communication across module boundaries, requirements on storage without heavy binding to a database management system vendor and frontend connection primarily when developing a backend based progressive web app (PWA).  

AI4E especially targets these problems. It is based on the Microsoft .Net Core technology and is intended to be used together with Asp.Net Core and Asp.Net Core Blazor, though many components are not bound to these frameworks and can be used in other scenarios aswell.

## Big picture
The ultimate goal is the evolution of the AI4E project into a software framework that manages and eases many aspects of enterprise software development. Templates for many common use-cases allows the adaption und usage of the framework to the users needs, so that the developers can concentrate on building the product's business logic that mostly contributes to its value and produce stable, scalable and easy to maintain software quickly.  
Secondary and infrastructural concerns are handles by the AI4E framework, as cross-cutting concerns, like user authentication and authorization is. The framework will manage persistence by a domain-focused storage engine based on a storage system abstraction that many providers are available. Modules are build like microservices, but can be installed and unintalled while the system is running and fully functioning. This allows to split a software product into multiple cohesive parts and let the users decide which ones are necessary and shall be installed. Modules communication via a build in and powerful message broker that is able to use various popular messaging solutions as backend. The modular Blazor extensions even allows to design the frontend in a modular way, so that each module can extend the user interface and allows interaction with the user.

### Design goals
Central goals of the project is a user-friendly, async-first, well documented API surface. The project is inherently based on dependency injection. It is highly configurable in a various amount of places and regards and made up of components designed for beeing replaced when custom functionality is needed.

### Current state
The project is currently in active development and in a phase of project redesign. A lot of project parts are implemented and relatively stable. The current effort concentrates on stabilizing and releasing a minimal basic set of features. This includes the storage engine with a single storage backend (MongoDB), an in-memory message broker and some basic Blazor functionality. Additionally a set of sample projects and tutorials together with a complete API description and project component description is in progress.

### Roadmap
Future development will incrementally add new features and enhance existing one. This will allow the project to be used by a much larger set of types of projects. The main features that will be added in the future are:  
* Server-side Blazor modularity extensions
* Blazor WASM modularity extensions
* Blazor WASM message broker support
* In process modularity (Modules are hosted all in the same process)
* Out of process modularity (Modules are hosted in a dedicated process each)
* Docker modularity (Modules are hosted in a docker container each)
* RabbitMQ message broker backend
* Storage metadata support
* Clustering support
* Automatic cloud scaling support

## Project parts
The AI4E project is split into multiple parts for convenience and flexibility, altough some parts depend on each other.

### AI4E.Utils
The AI4E.Utils are a set of utility libraries that contain often used functionaly that is organised in a dedicated set of library to allow developers to use these standalone. This included a large set of extension methods to existing types, as well as reflection utilities, helpers for memory based operations and async code.  
For further information see the readme [page of AI4E.Utils]. **TODO: Add link with further information**

### AI4E.Messaging
This is the message broker implementation that other parts of the project depend on. It provides a default implementation of the mediator design pattern, that is an in memory message broker, but the same provided API can be used as a full fledged message broker when a supported backend messaging service is used. Information on the messaging service is located on the [AI4E.Messaging readme page]. **TODO: Add link with further information**

### AI4E.Storage
AI4E.Storage provides an abstraction for database management systems. Using a supported database management system as backend allows a straighforward way of persistence and yout project to be independent of the backend database managemenet system, that can be replaced with ease. Build on top of this abstraction is a specialized domain storage engine, that loads, stores, manages domain entities and allows the guaranteed dispatch of domain events. When performance is a problem, a CQRS like projection mechanism is able to produce user-defined entity projections on entity storage. The AI4E.Storage readme page can be found [here]. **TODO: Add link with further information**

### AI4E.AspNetCore.Components
The Blazor extensions add some often used functionality to the Microsoft Asp.Net Core Blazor framework, like a generic extensible router implementation and a notification system. There is also support for extensibility, as this is the building block for future investments in modularity (See #Big picture).
The description of the lazor extensions can be found on the [AI4E.AspNetCore.Components readme page]. **TODO: Add link with further information**

## Getting started
To get started with the AI4E project, you can start with the [beginners tutorials] located in the project wiki or you can have a look at the samples accompanying the project. **TODO: Add links**

## Releases and package repository

Releases of the AI4E project are distributed via nuget packages. The nuget packages* can be loaded either from this [repository's GitHub packages](https://github.com/orgs/AI4E/packages?repo_name=AI4E) or from the [AI4E myget dev feed](https://www.myget.org/F/ai4e-dev).  
As there are currently no supported stable releases, no packages are pushed to nuget.org currently. 

*Please be aware that these are prerelease packages and not meant for production use.

To add the AI4E myget dev feed to a project, add the following either to a single project or a reachable `Directory.Build.props` file in a parent directory.

```
<PropertyGroup>
   <RestoreAdditionalProjectSources>$(RestoreAdditionalProjectSources);https://www.myget.org/F/ai4e-dev/api/v3/index.json</RestoreAdditionalProjectSources>
</PropertyGroup>
```
