# 💸 Kerzel Pay

A full-stack money transfer web application built with ASP.NET Core MVC for the **Dynamic Web** course at USJ — INCI 2026.

## 👥 Team
- **Anthony Kerbage**
- **Yorgo Moukarzel**

## 🛠 Tech Stack
- ASP.NET Core MVC (.NET 9)
- Entity Framework Core (Code-First)
- SQL Server (Express)
- ASP.NET Core Identity
- Repository Pattern + Dependency Injection
- Stripe (payment gateway)
- Leaflet.js (agent map)

## ✨ Features
### 👤 User
- Register / Login (email + social auth)
- Multi-currency accounts with unique serial numbers
- Stripe top-ups
- Beneficiaries management
- Account-to-account transfers + OMT-style transfers (by mobile)
- Automatic currency conversion
- Unique transaction tracking numbers
- Transaction history + receipts
- Reviews and ratings
- Real-time notifications
- Interactive map of all agents

### 🏪 Agent
- Register as a local partner store (admin approval)
- Manage cash-in / cash-out operations
- Track commissions
- Manage hours and location

### 🛠 Admin
- Approve / reject agents
- Manage commission structures
- Manage currencies and exchange rates
- Generate reports on platform usage

## 🚀 Getting Started

### Prerequisites
- .NET 9 SDK
- SQL Server (Express or full) + SSMS
- Visual Studio 2022 (17.12+)

### Setup
1. Clone the repo
```bash
   git clone https://github.com/Kerbagio/KerzelPay.git
```
2. Update the connection string in `appsettings.json` to match your SQL Server instance.
3. In **Package Manager Console**:
```powershell
   Update-Database
```
4. Press **F5** to run.

### Default credentials
| Role  | Email                  | Password   |
|-------|------------------------|------------|
| Admin | admin@kerzelpay.com    | Admin@123  |
| User  | user@kerzelpay.com     | User@123   |

## 📅 Project Timeline
Kickoff: 17/03/2026 — Presentation: last 2 sessions.

## 📜 License
Academic project — USJ INCI 2026.