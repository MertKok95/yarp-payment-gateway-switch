# Payment Gateway Security, Refund & Routing Policies (2026)

## 1. Refund & Chargeback Policy
- Standard transactions can be refunded within 14 days of processing.
- High-value transactions (over $5,000 or £4,000) require dual-authorization from the Risk Management API before a refund is issued.
- Chargeback disputes must be responded to within 72 hours with full 3D Secure logs.

## 2. Smart Routing & Failover Rules
- If Garanti BBVA POS experiences a timeout (>3000ms), YARP Gateway automatically reroutes traffic to Akbank POS.
- Ziraat Bank POS is the mandatory primary route for public sector and agricultural merchant category codes (MCC).

## 3. PCI-DSS & Security Compliance
- Credit card PAN numbers must never be logged in plain text; masking (e.g., 4543-XXXX-XXXX-1234) is strictly enforced.
- All AI Agent interactions are sanitized to prevent Prompt Injection attacks.