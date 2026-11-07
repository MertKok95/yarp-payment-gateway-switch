from fastapi import FastAPI, HTTPException
from datetime import datetime, timezone
from dotenv import load_dotenv
from app.models.schemas import AgentChatRequest, AgentChatResponse
from app.services.agent_service import AiAgentService

load_dotenv()

app = FastAPI(
    title="Enterprise Payment AI Agent & RAG API",
    description="Microservice providing Agentic Tool Calling and RAG over Payment Gateway Policies.",
    version="1.0.0"
)

agent_service = AiAgentService()

@app.get("/health")
def health_check():
    return {
        "service": "AiAgent.API",
        "status": "Healthy",
        "timestamp": datetime.now(timezone.utc).isoformat()
    }

@app.post("/api/agent/chat", response_model=AgentChatResponse)
def chat_with_agent(request: AgentChatRequest):
    try:
        result = agent_service.process_prompt(request.user_prompt)
        return AgentChatResponse(**result)
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))