from datetime import datetime
from typing import Optional

from pydantic import BaseModel, Field


class WithdrawCreate(BaseModel):
    ton_address: str = Field(..., min_length=10, max_length=128)
    amount: float = Field(..., gt=0)
    memo: Optional[str] = None


class WithdrawOut(BaseModel):
    id: int
    ton_address: str
    amount: float
    memo: Optional[str] = None
    created_at: datetime

    class Config:
        from_attributes = True
