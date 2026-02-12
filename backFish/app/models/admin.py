from sqlalchemy import Integer, String, Text
from sqlalchemy.orm import Mapped, mapped_column

from app.database import Base


class AdminData(Base):
    __tablename__ = "admindata"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    name: Mapped[str] = mapped_column(String(255), nullable=False)
    value: Mapped[str] = mapped_column(Text, nullable=False)
