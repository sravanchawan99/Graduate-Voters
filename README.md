TheVoter.in – Graduate MLC Voter Registration Platform
 
Overview
 
TheVoter.in is a web-based platform developed to simplify the graduate voter registration process for eligible graduates in Telangana. The platform provides an accessible interface for collecting and managing graduate voter registration information, helping streamline the enrollment process.
 
The application is built using Angular and Microsoft Azure cloud services, following a serverless architecture for backend processing, data management, and deployment.
 
Key Features
 
- Graduate voter registration with an intuitive user interface.
- Secure backend processing through Azure Functions.
- API-driven communication between frontend and backend.
- Voter record management using Azure Table Storage.
- CSV export functionality using Azure Blob Storage.
- Event-driven processing using Azure Service Bus triggers.
- HTTP-triggered Azure Functions for API operations.
- Timer-triggered Azure Functions for scheduled background tasks.
- Automated CI/CD deployment using GitHub Actions.
 
Technology Stack
 
Component| Technology
Frontend| Angular
Backend| Azure Functions
Cloud Hosting| Azure Static Web Apps
Database| Azure Table Storage
File Storage| Azure Blob Storage
Messaging| Azure Service Bus
API Integration| HTTP Triggers
Scheduled Processing| Timer Triggers
CI/CD| GitHub Actions
Cloud Platform| Microsoft Azure
 
Architecture
 
The application follows a serverless cloud architecture:
 
1. Frontend: Angular provides the user interface for graduate voter registration.
2. Backend: Azure Functions handles API requests and business logic.
3. Data Management: Azure Table Storage maintains voter registration records.
4. File Management: Azure Blob Storage supports CSV exports.
5. Event Processing: Azure Service Bus enables asynchronous message processing.
6. Scheduled Operations: Timer-triggered functions handle background tasks.
7. Deployment: GitHub Actions automates build and deployment workflows.
 
Deployment
 
The frontend is hosted on Azure Static Web Apps, while backend services are powered by Azure Functions.
 
CI/CD pipelines are configured using GitHub Actions to automate application build and deployment processes.
 
Live Application
 
🌐 https://www.thevoter.in
 
Disclaimer
 
This is an independent technology initiative developed to simplify the graduate voter registration process. It is not an official Election Commission of India website or government portal. Voter eligibility, registration and final enrollment are subject to the applicable official election procedures.
 
---
 
Developed using Angular and Microsoft Azure.
