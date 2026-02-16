from datetime import datetime
from typing import Optional

from pydantic import BaseModel, Field


class TransactionCreate(BaseModel):
    tgid: str = Field(..., min_length=1, max_length=100)
    username: Optional[str] = Field(default=None, max_length=100)
    action: str = Field(..., min_length=1, max_length=64)
    amount: Optional[float] = None
    currency: Optional[str] = Field(default=None, max_length=16)
    details: Optional[str] = None


class TransactionOut(BaseModel):
    id: int
    tgid: str
    username: Optional[str] = None
    action: str
    amount: Optional[float] = None
    currency: Optional[str] = None
    details: Optional[str] = None
    created_at: datetime

    class Config:
        from_attributes = True
