# DotNetCloud App Planning Checklist

## Milestones
- [X] Database Schema & Core Models: Design and implement tables for users, teams, roles, and settings
- [x] Authentication & Authorization: Implement user registration, login, password hashing/salting, and JWT authentication
- [ ] Team Management: Create, edit, delete teams; assign users to teams; set team permissions
- [ ] File Management: Enable file upload/download; implement team-based file access
- [ ] API Development: Build main API endpoints for authentication, teams, files, and settings
- [ ] Blazor Web UI: Develop core UI for login, dashboard, file management, and team management
- [ ] Desktop Client: Implement Avalonia client for file sync and management
- [ ] Notifications & Activity Logging: Add user notifications and activity tracking
- [ ] Extensibility: Set up plugin/module system for future features
- [ ] Testing & Quality: Write and run unit/integration tests for all major components
- [ ] Deployment & Documentation: Prepare for production deployment; document setup and usage

This document tracks planned and completed functionality for the NetCloud application.

## Security & Access
- [ ] User authentication and authorization (implement from scratch with salting and hashing; requires new tables in NetCloud DB)
- [ ] JWT authentication for API
- [ ] Role-based access control
- [ ] Team-based access (ensure only correct users/teams can access files they own)

## Teams & Collaboration
- [ ] Team (group) management (create, edit, delete teams)
- [ ] Assign users to teams
- [ ] Team-based file sharing and permissions

## Core Functionality
- [ ] File upload and download
- [ ] Settings management
- [ ] Admin dashboard
- [ ] Activity logging
- [ ] Notifications system
- [ ] API endpoints for client apps

## UI & Clients
- [ ] Blazor UI enhancements
- [ ] DotNetCloud.Server.Web (Blazor WebAssembly front-end for API)
- [ ] DotNetCloud.Client.Avalonia (desktop client using Avalonia UI; sync directories with server)

## Data & Services
- [ ] DotNetCloud.Server.Data (database interactions and models)
- [ ] DotNetCloud.Server.Services (business logic and services)
- [ ] DotNetCloud.Core (shared models and utilities)

## Testing
- [ ] DotNetCloud.Server.Web.Tests (unit/integration tests for web app)
- [ ] DotNetCloud.Server.Api.Tests (API endpoint tests)
- [ ] DotNetCloud.Server.Services.Tests (services/business logic tests)
- [ ] DotNetCloud.Server.Data.Tests (database interaction tests)
- [ ] DotNetCloud.Core.Tests (shared models/utilities tests)

## Extensibility
- [ ] Setup for functionality extensions via plugins or modules

## Completed Features
- [ ] (Add completed features here)

## Notes
- Update this checklist as features are added, changed, or completed.
- Use this file for team planning and progress tracking.
