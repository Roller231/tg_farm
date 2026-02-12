from sqlalchemy import Integer, Numeric, String
from sqlalchemy.orm import Mapped, mapped_column

from app.database import Base


class LevelReward(Base):
    __tablename__ = "level_rewards"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    level: Mapped[int] = mapped_column(Integer, nullable=False)
    type: Mapped[str] = mapped_column(String(50), nullable=False)
    amount: Mapped[float] = mapped_column(Numeric(12, 2), nullable=False)
    isPremium: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
