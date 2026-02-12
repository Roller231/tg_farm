from pydantic import BaseModel


class RewardCreate(BaseModel):
    level: int
    type: str
    amount: float
    isPremium: int = 0


class RewardOut(BaseModel):
    id: int
    level: int
    type: str
    amount: float
    isPremium: int

    class Config:
        from_attributes = True
