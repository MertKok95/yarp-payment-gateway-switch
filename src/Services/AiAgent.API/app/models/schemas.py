from pydantic import BaseModel, Field

class AgentChatRequest(BaseModel):
    user_prompt: str = Field(..., description="The question or command for the AI Agent.")
    session_id: str = Field(default="default-session", description="Tracking ID for conversation state.")

class AgentChatResponse(BaseModel):
    isSuccess: bool
    provider: str
    mode: str
    tool_used: str | None = None
    response: str