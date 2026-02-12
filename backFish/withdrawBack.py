from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field
from typing import Optional
from sqlalchemy import create_engine, Column, Integer, String, Float, TIMESTAMP, func
from sqlalchemy.orm import declarative_base, sessionmaker, Mapped, mapped_column
import os

# -------------------- DB setup --------------------
DATABASE_URL = "mysql+pymysql://root:141722@localhost:3306/farm_game?charset=utf8mb4"
engine = create_engine(
    DATABASE_URL,
    echo=False,
    future=True,
    pool_pre_ping=True,
)
SessionLocal = sessionmaker(bind=engine, autoflush=False, autocommit=False, future=True)
Base = declarative_base()

# -------------------- ORM модель --------------------
class WithdrawRequest(Base):
    __tablename__ = "withdraw_requests"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    memo: Mapped[Optional[str]] = mapped_column(String(255), nullable=True)
    ton_address: Mapped[str] = mapped_column(String(128), nullable=False)
    amount: Mapped[float] = mapped_column(Float, nullable=False)
    created_at: Mapped[str] = mapped_column(
        TIMESTAMP, nullable=False, server_default=func.now()
    )

Base.metadata.create_all(engine)

# -------------------- Pydantic схема --------------------
class WithdrawCreate(BaseModel):
    ton_address: str = Field(..., min_length=10, max_length=128)
    amount: float = Field(..., gt=0)
    memo: Optional[str] = None

class WithdrawOut(BaseModel):
    id: int
    ton_address: str
    amount: float
    memo: Optional[str]
    created_at: str

    class Config:
        from_attributes = True

# -------------------- FastAPI App --------------------
app = FastAPI(title="Withdraw API", version="1.0.0")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"], allow_credentials=True,
    allow_methods=["*"], allow_headers=["*"]
)

# -------------------- Endpoint --------------------
@app.post("/withdraw", response_model=WithdrawOut, status_code=201)
def create_withdraw(payload: WithdrawCreate):
    with SessionLocal() as db:
        req = WithdrawRequest(
            ton_address=payload.ton_address,
            amount=payload.amount,
            memo=payload.memo
        )
        db.add(req)
        db.commit()
        db.refresh(req)
        return req

# Запуск:
# uvicorn withdrawBack:app --port 8005 --reload


