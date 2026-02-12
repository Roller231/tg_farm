from fastapi import FastAPI, HTTPException, Path
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import List
from sqlalchemy import create_engine, Integer, String, Numeric
from sqlalchemy.orm import declarative_base, sessionmaker, Mapped, mapped_column
import os

# ---------------- DB ----------------
DATABASE_URL = os.getenv(
    "USERS_DATABASE_URL",
    "mysql+pymysql://root:141722@localhost:3306/farm_game?charset=utf8mb4"
)

engine = create_engine(DATABASE_URL, echo=False, future=True, pool_pre_ping=True)
SessionLocal = sessionmaker(bind=engine, autoflush=False, autocommit=False, future=True)
Base = declarative_base()

# ---------------- ORM ----------------
class LevelReward(Base):
    __tablename__ = "level_rewards"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    level: Mapped[int] = mapped_column(Integer, nullable=False)
    type: Mapped[str] = mapped_column(String(50), nullable=False)
    amount: Mapped[float] = mapped_column(Numeric(12, 2), nullable=False)  # ← Decimal в БД, но мапим как float
    isPremium: Mapped[int] = mapped_column(Integer, nullable=False, default=0)

Base.metadata.create_all(engine)

# ---------------- Schemas ----------------
class RewardCreate(BaseModel):
    level: int
    type: str
    amount: float           # ← было Decimal, стало float
    isPremium: int = 0

class RewardOut(BaseModel):
    id: int
    level: int
    type: str
    amount: float           # ← было Decimal, стало float
    isPremium: int

    class Config:
        from_attributes = True


# ---------------- FastAPI ----------------
app = FastAPI(title="Level Rewards API", version="1.0.0")
app.add_middleware(
    CORSMiddleware, allow_origins=["*"], allow_credentials=True,
    allow_methods=["*"], allow_headers=["*"]
)


# CRUD
@app.post("/rewards", response_model=RewardOut, status_code=201)
def create_reward(payload: RewardCreate):
    with SessionLocal() as db:
        reward = LevelReward(**payload.model_dump())
        db.add(reward)
        db.commit()
        db.refresh(reward)
        return reward


@app.get("/rewards/{reward_id}", response_model=RewardOut)
def get_reward(reward_id: int = Path(...)):
    with SessionLocal() as db:
        reward = db.get(LevelReward, reward_id)
        if not reward:
            raise HTTPException(404, "Reward not found")
        return reward


@app.get("/rewards", response_model=List[RewardOut])
def list_rewards():
    with SessionLocal() as db:
        return db.query(LevelReward).all()


@app.delete("/rewards/{reward_id}", status_code=204)
def delete_reward(reward_id: int):
    with SessionLocal() as db:
        reward = db.get(LevelReward, reward_id)
        if not reward:
            raise HTTPException(404, "Reward not found")
        db.delete(reward)
        db.commit()
