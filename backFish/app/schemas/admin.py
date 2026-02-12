from typing import Optional

from pydantic import BaseModel


class AdminDataCreate(BaseModel):
    name: str
    value: str


class AdminDataUpdate(BaseModel):
    name: Optional[str] = None
    value: Optional[str] = None


class AdminDataOut(BaseModel):
    id: int
    name: str
    value: str

    class Config:
        from_attributes = True
