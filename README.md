# YARP Payment Gateway & Settlement Switch

An enterprise-grade, high-performance Payment Gateway and Settlement Switch built with **.NET 10** and **YARP (Yet Another Reverse Proxy)**. This distributed architecture simulates a highly concurrent payment processing environment, featuring dynamic BIN routing, Luhn validation, external provider integration (Iyzico), and inter-service settlement mechanics.

## 🏗 Architecture Overview

The system operates as a reverse proxy that intercepts payment requests, inspects the payload, and dynamically routes traffic to the appropriate banking microservice based on the Bank Identification Number (BIN) and network rules.

- **PaymentGateway.Yarp (Port 5000):** The core routing engine. Implements Rate Limiting, Idempotency checks, Luhn Algorithm validation, and dynamic header injection (`X-Target-Bank`).
- **Garanti.API (Port 5001):** Acquirer microservice integrated with the official **Iyzico .NET SDK** for external live/sandbox processing.
- **Akbank.API (Port 5002):** Acquirer microservice and internal **Merchant Ledger / Settlement Engine**.
- **Ziraat.API (Port 5003):** Fallback Acquirer microservice.
- **AiAgent.API (Port 5004):** Python and FastAPI microservice providing an LLM-based AI Agent. Implements **LangChain** for **RAG (Retrieval-Augmented Generation)** over corporate payment policies and **Tool Calling** for live bank commission queries via Groq and OpenAI.

## 🚀 Key Enterprise Features

* **Dynamic BIN Routing:** Inspects the first 6 digits of the PAN to route traffic to the correct acquiring bank cluster without client awareness.
* **Idempotency & Rate Limiting:** Prevents duplicate charges via concurrent dictionary caching and protects against volumetric attacks (500 req/sec limit).
* **PCI-DSS Compliance Simulation:** Implements PAN masking in application logs and responses.
* **Automated Settlement:** Deducts POS commission and automatically dispatches the net payout via HTTP to the merchant's target IBAN ledger.
* **Agentic AI & RAG Workflow:** Autonomous tool execution for bank commission queries and semantic document search over internal refund, failover, and security policies, with multi-provider support (Groq/OpenAI) and graceful fallback simulation.

## 🛠 Tech Stack
- **Framework:** .NET 10 (ASP.NET Core Web API)
- **Proxy:** Microsoft YARP (Reverse Proxy)
- **AI & RAG Microservice:** Python 3.11, FastAPI, LangChain, Groq, OpenAI, Pydantic
- **Integration:** Iyzico Official SDK (`Iyzipay`)
- **CI/CD:** GitHub Actions

## 🚦 How to Run

1. Clone the repository.
2. Update `src/Services/Garanti.API/appsettings.json` with your Iyzico Sandbox API keys if external routing is desired.
3. For the AI Agent service, copy `src/Services/AiAgent.API/.env.example` to `src/Services/AiAgent.API/.env` and configure your `GROQ_API_KEY` or `OPENAI_API_KEY`.
4. Build and run all microservices concurrently:

```bash
dotnet build
# Start .NET services in separate terminal instances:
dotnet run --project src/Gateway/PaymentGateway.Yarp/PaymentGateway.Yarp.csproj
dotnet run --project src/Services/Garanti.API/Garanti.API.csproj
dotnet run --project src/Services/Akbank.API/Akbank.API.csproj
dotnet run --project src/Services/Ziraat.API/Ziraat.API.csproj

# Start Python AI Agent microservice in a new terminal instance:
cd src/Services/AiAgent.API
pip install -r requirements.txt
uvicorn app.main:app --port 5004 --reload