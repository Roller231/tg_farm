from decimal import Decimal

from sqlalchemy import Integer, Numeric, String, Text
from sqlalchemy.orm import Mapped, mapped_column

from app.database import Base


class Product(Base):
    __tablename__ = "products"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    name: Mapped[str] = mapped_column(String(100), nullable=False)
    type: Mapped[str] = mapped_column(String(50), nullable=False, default="")
    price: Mapped[Decimal] = mapped_column(Numeric(10, 2), nullable=False)
    sell_price: Mapped[Decimal] = mapped_column(Numeric(10, 2), nullable=False)
    speed_price: Mapped[Decimal] = mapped_column(Numeric(10, 2), nullable=False)
    lvl_for_buy: Mapped[int] = mapped_column(Integer, nullable=False)
    time: Mapped[int] = mapped_column(Integer, nullable=False)
    exp: Mapped[Decimal] = mapped_column(Numeric(10, 4), nullable=False, default=Decimal("0"))
    image_seed_link: Mapped[str] = mapped_column(Text, nullable=False)
    image_ready_link: Mapped[str] = mapped_column(Text, nullable=False)
