from decimal import Decimal

from sqlalchemy import Integer, Numeric, String, Text
from sqlalchemy.orm import Mapped, mapped_column

from app.database import Base


class Task(Base):
    """
    Динамические задания.

    type — тип задания:
        "money"   — накопить монет (count_to_check)
        "bezoz"   — накопить безосов
        "lvl"     — достичь уровня
        "cell"    — открыть N клеток
        "channel" — подписаться на Telegram-канал (channel_url)

    reward_type — валюта награды: "coin", "bezoz", "lvl", "ton"
    reward_amount — количество награды
    channel_url — ссылка на канал (только для type="channel")
    channel_id  — числовой ID или @username канала для проверки через бота (только для type="channel")
    """
    __tablename__ = "tasks"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    title: Mapped[str] = mapped_column(String(255), nullable=False)
    description: Mapped[str] = mapped_column(Text, nullable=False, default="")
    type: Mapped[str] = mapped_column(String(50), nullable=False)  # money/bezoz/lvl/cell/channel
    count_to_check: Mapped[float] = mapped_column(Numeric(12, 2), nullable=False, default=Decimal("0"))
    reward_type: Mapped[str] = mapped_column(String(50), nullable=False)  # coin/bezoz/lvl/ton
    reward_amount: Mapped[float] = mapped_column(Numeric(12, 2), nullable=False, default=Decimal("0"))
    channel_url: Mapped[str] = mapped_column(Text, nullable=True, default=None)
    channel_id: Mapped[str] = mapped_column(String(255), nullable=True, default=None)
