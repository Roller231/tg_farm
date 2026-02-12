from typing import List

from fastapi import APIRouter, Depends, HTTPException, Path
from sqlalchemy.orm import Session

from app.database import get_db
from app.models.level_reward import LevelReward
from app.schemas.level_reward import RewardCreate, RewardOut

router = APIRouter(prefix="/rewards", tags=["Level Rewards"])


@router.post("", response_model=RewardOut, status_code=201)
def create_reward(payload: RewardCreate, db: Session = Depends(get_db)):
    reward = LevelReward(**payload.model_dump())
    db.add(reward)
    db.commit()
    db.refresh(reward)
    return reward


@router.get("", response_model=List[RewardOut])
def list_rewards(db: Session = Depends(get_db)):
    return db.query(LevelReward).all()


@router.get("/{reward_id}", response_model=RewardOut)
def get_reward(reward_id: int = Path(...), db: Session = Depends(get_db)):
    reward = db.get(LevelReward, reward_id)
    if not reward:
        raise HTTPException(status_code=404, detail="Reward not found")
    return reward


@router.delete("/{reward_id}", status_code=204)
def delete_reward(reward_id: int = Path(...), db: Session = Depends(get_db)):
    reward = db.get(LevelReward, reward_id)
    if not reward:
        raise HTTPException(status_code=404, detail="Reward not found")
    db.delete(reward)
    db.commit()
