from decimal import Decimal
from typing import Any, Literal, Optional

from pydantic import BaseModel, Field, constr

IdStr = constr(strip_whitespace=True, min_length=1, max_length=100)
NameStr = constr(strip_whitespace=True, min_length=1, max_length=100)


class UserCreate(BaseModel):
    id: IdStr
    name: NameStr
    firstName: Optional[str] = None

    ton: float = 0
    lvl_upgrade: float = 0
    lvl: int = 1
    coin: Decimal = Field(default=Decimal("100"))
    bezoz: Decimal = Field(default=Decimal("10"))
    ref_count: int = 0
    refId: Optional[str] = None

    isPremium: int = 0

    blocked: int = 0

    time_farm: str = ""
    seed_count: str = ""
    storage_count: str = ""
    grid_count: int = 3
    grid_state: str = ""

    houses: str = ""


class UserUpdate(BaseModel):
    name: Optional[NameStr] = None
    firstName: Optional[str] = None

    ton: Optional[float] = None
    lvl_upgrade: Optional[float] = None
    lvl: Optional[int] = None
    coin: Optional[Decimal] = None
    bezoz: Optional[Decimal] = None
    ref_count: Optional[int] = None
    refId: Optional[str] = None

    isPremium: Optional[int] = None

    blocked: Optional[int] = None

    time_farm: Optional[str] = None
    seed_count: Optional[str] = None
    storage_count: Optional[str] = None
    grid_count: Optional[int] = None
    grid_state: Optional[str] = None

    houses: Optional[str] = None

    def any_set(self) -> bool:
        return bool(self.model_dump(exclude_unset=True))


AllowedField = Literal[
    "name", "firstName", "ton", "lvl_upgrade", "lvl", "coin", "bezoz",
    "ref_count", "refId", "isPremium",
    "blocked",
    "time_farm", "seed_count", "storage_count", "grid_count", "grid_state",
    "houses",
]


class SingleFieldUpdate(BaseModel):
    field: AllowedField
    value: Any


class UserOut(BaseModel):
    id: str
    name: str
    firstName: Optional[str] = None

    ton: float
    lvl_upgrade: float
    lvl: int
    coin: Decimal
    bezoz: Decimal
    ref_count: int
    refId: Optional[str] = None

    isPremium: int

    blocked: int

    time_farm: str
    seed_count: str
    storage_count: str
    grid_count: int
    grid_state: str

    houses: str

    class Config:
        from_attributes = True
