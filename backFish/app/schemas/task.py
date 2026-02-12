from typing import Optional

from pydantic import BaseModel


class TaskCreate(BaseModel):
    title: str
    description: str = ""
    type: str  # money / bezoz / lvl / cell / channel
    count_to_check: float = 0
    reward_type: str  # coin / bezoz / lvl / ton
    reward_amount: float = 0
    channel_url: Optional[str] = None
    channel_id: Optional[str] = None


class TaskUpdate(BaseModel):
    title: Optional[str] = None
    description: Optional[str] = None
    type: Optional[str] = None
    count_to_check: Optional[float] = None
    reward_type: Optional[str] = None
    reward_amount: Optional[float] = None
    channel_url: Optional[str] = None
    channel_id: Optional[str] = None


class TaskOut(BaseModel):
    id: int
    title: str
    description: str
    type: str
    count_to_check: float
    reward_type: str
    reward_amount: float
    channel_url: Optional[str] = None
    channel_id: Optional[str] = None

    class Config:
        from_attributes = True
