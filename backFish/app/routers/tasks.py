from typing import List

import httpx
from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy import delete, select, update
from sqlalchemy.orm import Session

from app.database import get_db
from app.models.task import Task
from app.schemas.task import TaskCreate, TaskOut, TaskUpdate

# Токен бота — используется для проверки подписки на канал через Telegram Bot API
BOT_TOKEN = "8432053231:AAG7Bq4NUgguRefZLh2pBLoJL0pGKtg-HFs"

router = APIRouter(prefix="/tasks", tags=["Tasks"])


# ==================== CRUD ====================

@router.get("", response_model=List[TaskOut])
def get_all_tasks(db: Session = Depends(get_db)):
    """Получить все задания (Unity загружает этот список)."""
    rows = db.execute(select(Task)).scalars().all()
    return rows


@router.get("/{task_id}", response_model=TaskOut)
def get_task(task_id: int, db: Session = Depends(get_db)):
    obj = db.get(Task, task_id)
    if not obj:
        raise HTTPException(status_code=404, detail="Task not found")
    return obj


@router.post("", response_model=TaskOut, status_code=201)
def create_task(payload: TaskCreate, db: Session = Depends(get_db)):
    """Создать новое задание. Вызывай из админки или напрямую через API."""
    obj = Task(**payload.model_dump())
    db.add(obj)
    db.commit()
    db.refresh(obj)
    return obj


@router.put("/{task_id}")
def update_task(task_id: int, payload: TaskUpdate, db: Session = Depends(get_db)):
    fields = payload.model_dump(exclude_unset=True)
    if not fields:
        raise HTTPException(status_code=400, detail="No valid fields")
    stmt = update(Task).where(Task.id == task_id).values(**fields)
    res = db.execute(stmt)
    db.commit()
    if res.rowcount == 0:
        raise HTTPException(status_code=404, detail="Task not found")
    return {"updated": True}


@router.delete("/{task_id}", status_code=204)
def delete_task(task_id: int, db: Session = Depends(get_db)):
    stmt = delete(Task).where(Task.id == task_id)
    res = db.execute(stmt)
    db.commit()
    if res.rowcount == 0:
        raise HTTPException(status_code=404, detail="Task not found")


# ==================== Проверка подписки на канал ====================

@router.get("/check-channel/{task_id}")
async def check_channel_subscription(
    task_id: int,
    user_id: str = Query(..., description="Telegram user ID"),
    db: Session = Depends(get_db),
):
    """
    Проверяет, подписан ли user_id на канал, указанный в задании task_id.
    Возвращает {"subscribed": true/false}.
    """
    task_obj = db.get(Task, task_id)
    if not task_obj:
        raise HTTPException(status_code=404, detail="Task not found")
    if task_obj.type != "channel":
        raise HTTPException(status_code=400, detail="Task is not a channel task")
    if not task_obj.channel_id:
        raise HTTPException(status_code=400, detail="channel_id not configured for this task")

    url = f"https://api.telegram.org/bot{BOT_TOKEN}/getChatMember"
    params = {"chat_id": task_obj.channel_id, "user_id": user_id}

    async with httpx.AsyncClient() as client:
        resp = await client.get(url, params=params)

    if resp.status_code != 200:
        return {"subscribed": False, "error": "Telegram API error"}

    data = resp.json()
    if not data.get("ok"):
        return {"subscribed": False, "error": data.get("description", "unknown")}

    status = data["result"].get("status", "left")
    # member / administrator / creator — подписан; left / kicked — нет
    subscribed = status in ("member", "administrator", "creator")
    return {"subscribed": subscribed}
