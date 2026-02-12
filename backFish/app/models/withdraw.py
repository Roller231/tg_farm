from datetime import datetime
from typing import Optional

from sqlalchemy import TIMESTAMP, Float, Integer, String, func
from sqlalchemy.orm import Mapped, mapped_column

from app.database import Base


class WithdrawRequest(Base):
    __tablename__ = "withdraw_requests"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    memo: Mapped[Optional[str]] = mapped_column(String(255), nullable=True)
    ton_address: Mapped[str] = mapped_column(String(128), nullable=False)
    amount: Mapped[float] = mapped_column(Float, nullable=False)
    created_at: Mapped[datetime] = mapped_column(
        TIMESTAMP, nullable=False, server_default=func.now()
    )
