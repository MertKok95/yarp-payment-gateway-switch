import os
from pathlib import Path
from typing import Dict, Any, Tuple, Optional
from langchain_core.tools import tool
from langchain_core.messages import HumanMessage, SystemMessage
from langchain_core.language_models.chat_models import BaseChatModel


@tool
def get_bank_commission_rate(bank_name: str) -> str:
    """
    Returns the live POS commission rate for a specific bank (e.g., Akbank, Garanti, Ziraat).
    Call this tool when the user asks about commission rates, pricing, or fee percentages.
    """
    bank = bank_name.lower()
    if "akbank" in bank:
        return "1.89%"
    elif "garanti" in bank:
        return "2.15%"
    elif "ziraat" in bank:
        return "1.45%"
    return "Standard rate is 2.00%"


@tool
def search_payment_policies_rag(query: str) -> str:
    """
    Searches internal company documentation for refund policies, chargebacks, failover routing, and PCI-DSS security rules.
    Call this tool when the user asks about company policies, refunds, timeouts, or security rules.
    """
    doc_path = Path(__file__).parent.parent / "knowledge_base" / "payment_policies.md"
    if not doc_path.exists():
        return "Policy document not found."

    content = doc_path.read_text(encoding="utf-8")
    paragraphs = content.split("## ")
    relevant_chunks = [
        p for p in paragraphs 
        if any(word in p.lower() for word in query.lower().split() if len(word) > 3)
    ]

    if relevant_chunks:
        return "\n\n".join(relevant_chunks)
    return content


class AiAgentService:
    def __init__(self):
        self.tools = [get_bank_commission_rate, search_payment_policies_rag]

    def _get_llm(self) -> Tuple[Optional[BaseChatModel], str]:
        openai_key = os.getenv("OPENAI_API_KEY", "")
        groq_key = os.getenv("GROQ_API_KEY", "")

        if openai_key and openai_key.startswith("sk-") and len(openai_key) > 20:
            from langchain_openai import ChatOpenAI
            return ChatOpenAI(model="gpt-4o-mini", temperature=0, api_key=openai_key), "OpenAI (gpt-4o-mini)"

        if groq_key and groq_key.startswith("gsk_"):
            from langchain_groq import ChatGroq
            model_name = os.getenv("LLM_MODEL", "openai/gpt-oss-120b")
            return ChatGroq(model=model_name, temperature=0, api_key=groq_key), f"Groq ({model_name})"

        return None, "Simulation (Fallback Mode)"

    def process_prompt(self, user_prompt: str) -> Dict[str, Any]:
        llm, provider_name = self._get_llm()

        if llm is None:
            return {
                "isSuccess": True,
                "provider": provider_name,
                "mode": "Fallback Simulation",
                "tool_used": "get_bank_commission_rate",
                "response": "Simulated AI Response: Akbank POS commission is 1.89% and refunds take up to 14 days."
            }

        llm_with_tools = llm.bind_tools(self.tools)

        messages = [
            SystemMessage(
                content="You are an enterprise Payment Gateway AI Assistant. Always use the provided tools to look up bank commission rates or company policies before answering."
            ),
            HumanMessage(content=user_prompt)
        ]

        ai_msg = llm_with_tools.invoke(messages)

        if ai_msg.tool_calls:
            tool_call = ai_msg.tool_calls[0]
            tool_name = tool_call["name"]
            tool_args = tool_call["args"]

            if tool_name == "get_bank_commission_rate":
                tool_output = get_bank_commission_rate.invoke(tool_args)
            elif tool_name == "search_payment_policies_rag":
                tool_output = search_payment_policies_rag.invoke(tool_args)
            else:
                tool_output = "Tool execution completed."

            final_prompt = [
                SystemMessage(
                    content="Answer the user's question clearly and professionally using ONLY the retrieved context below."
                ),
                HumanMessage(
                    content=f"User Question: {user_prompt}\n\nRetrieved Context from Tool ({tool_name}):\n{tool_output}"
                )
            ]
            final_response = llm.invoke(final_prompt)

            return {
                "isSuccess": True,
                "provider": provider_name,
                "mode": "Agentic Workflow (RAG + Tool Calling)",
                "tool_used": tool_name,
                "response": final_response.content
            }

        return {
            "isSuccess": True,
            "provider": provider_name,
            "mode": "Direct LLM Chat",
            "tool_used": None,
            "response": ai_msg.content
        }