# AI4E
The 'Application Infrastructure for the Enterprise' library extends Asp.Net Core with functionality needed in medium to larger sized enterprise applications.<br>
It provides a messaging mechanism to decouple MVC-controllers that manage the views responsibilities from reusable application services. The integrated storage engine is built on top of modern application architecture like command query responsibility segration (CQRS), No-Sql and event-sourcing. With this in place a high level of persistence ignorance is achieved with the projects goal to completely decouple the domain model design from architectural and storage purposes. The library includes a modularity model that enables the application to be split into multiple modules (or microservices) that all run in isolation and are connected via the messaging mechanism.

## Project status
The project is in active development currently, with the goal to release the first release candidate (RC1) in the first quarter of 2018.

## Project goals
The project main goal is to deliver a high-level framework to easely build and maintain complex business applications in a modern, domain-driven and modular way. There is also the second goal to give advice and support in common situations learned from real-worl projects.

## Getting started
Currently there is no release on nuget or another packet manager. So feel free to clone the project locally. If the project is more stable, this is subject to change.

## Roadmap

Currently there is no release, so the goal is to build a stable, manufacturing ready release with the core features only.

## Schedule

| Time frame | Release |
|---|---|
| Q1 2018 | RC 1 |
| Q2 2018 | RC 2 |
| Q2/Q3 2018 | RTM 1.0|
