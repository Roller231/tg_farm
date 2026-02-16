from datetime import datetime
from typing import Optional

from sqlalchemy import TIMESTAMP, Float, Integer, String, Text, func
from sqlalchemy.orm import Mapped, mapped_column

from app.database import Base


class TransactionLog(Base):
    __tablename__ = "transactions"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    tgid: Mapped[str] = mapped_column(String(100), nullable=False, index=True)
    username: Mapped[Optional[str]] = mapped_column(String(100), nullable=True)

    action: Mapped[str] = mapped_column(String(64), nullable=False, index=True)
    amount: Mapped[Optional[float]] = mapped_column(Float, nullable=True)
    currency: Mapped[Optional[str]] = mapped_column(String(16), nullable=True)
    details: Mapped[Optional[str]] = mapped_column(Text, nullable=True)

    created_at: Mapped[datetime] = mapped_column(
        TIMESTAMP, nullable=False, server_default=func.now(), index=True
    )
