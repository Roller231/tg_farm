from decimal import Decimal
from typing import Optional

from sqlalchemy import Float, Integer, Numeric, String, Text
from sqlalchemy.orm import Mapped, mapped_column

from app.database import Base


class User(Base):
    __tablename__ = "users"

    id: Mapped[str] = mapped_column(String(100), primary_key=True)
    name: Mapped[str] = mapped_column(String(100), nullable=False)
    firstName: Mapped[Optional[str]] = mapped_column(String(100), nullable=True, default=None)

    ton: Mapped[float] = mapped_column(Float, nullable=False, default=0.0)
    lvl_upgrade: Mapped[float] = mapped_column(Float, nullable=False, default=0.0)
    lvl: Mapped[int] = mapped_column(Integer, nullable=False, default=1)
    coin: Mapped[Decimal] = mapped_column(Numeric(12, 2), nullable=False, default=Decimal("100"))
    bezoz: Mapped[Decimal] = mapped_column(Numeric(12, 2), nullable=False, default=Decimal("10"))
    ref_count: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    refId: Mapped[Optional[str]] = mapped_column(String(255), nullable=True, default=None)

    isPremium: Mapped[int] = mapped_column(Integer, nullable=False, default=0)

    time_farm: Mapped[str] = mapped_column(Text, nullable=False, default="")
    seed_count: Mapped[str] = mapped_column(Text, nullable=False, default="")
    storage_count: Mapped[str] = mapped_column(Text, nullable=False, default="")
    grid_count: Mapped[int] = mapped_column(Integer, nullable=False, default=3)
    grid_state: Mapped[str] = mapped_column(Text, nullable=False, default="")

    houses: Mapped[str] = mapped_column(Text, nullable=False, default="")
