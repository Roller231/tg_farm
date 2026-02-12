from decimal import Decimal
from typing import Any, Literal, Optional

from pydantic import BaseModel, Field, constr

NameStr = constr(strip_whitespace=True, min_length=1, max_length=100)


class ProductCreate(BaseModel):
    name: NameStr
    type: str = ""
    price: Decimal
    sell_price: Decimal
    speed_price: Decimal
    lvl_for_buy: int
    time: int
    exp: Decimal = Field(default=Decimal("0"))
    image_seed_link: str
    image_ready_link: str


class ProductUpdate(BaseModel):
    name: Optional[NameStr] = None
    type: Optional[str] = None
    price: Optional[Decimal] = None
    sell_price: Optional[Decimal] = None
    speed_price: Optional[Decimal] = None
    lvl_for_buy: Optional[int] = None
    time: Optional[int] = None
    exp: Optional[Decimal] = None
    image_seed_link: Optional[str] = None
    image_ready_link: Optional[str] = None

    def any_set(self) -> bool:
        return bool(self.model_dump(exclude_unset=True))


AllowedField = Literal[
    "name", "type", "price", "sell_price", "speed_price",
    "lvl_for_buy", "time", "exp",
    "image_seed_link", "image_ready_link",
]


class SingleFieldUpdate(BaseModel):
    field: AllowedField
    value: Any


class ProductOut(BaseModel):
    id: int
    name: str
    type: str
    price: Decimal
    sell_price: Decimal
    speed_price: Decimal
    lvl_for_buy: int
    time: int
    exp: Decimal
    image_seed_link: str
    image_ready_link: str

    class Config:
        from_attributes = True
