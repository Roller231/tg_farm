from typing import List

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import delete, select, update
from sqlalchemy.orm import Session

from app.database import get_db
from app.models.admin import AdminData
from app.schemas.admin import AdminDataCreate, AdminDataOut, AdminDataUpdate

router = APIRouter(prefix="/admindata", tags=["Admin Data"])


@router.get("", response_model=List[AdminDataOut])
def get_all_admin_data(db: Session = Depends(get_db)):
    rows = db.execute(select(AdminData)).scalars().all()
    return rows


@router.get("/{admin_id}", response_model=AdminDataOut)
def get_admin_data(admin_id: int, db: Session = Depends(get_db)):
    obj = db.get(AdminData, admin_id)
    if not obj:
        raise HTTPException(status_code=404, detail="AdminData not found")
    return obj


@router.post("", response_model=AdminDataOut, status_code=201)
def create_admin_data(payload: AdminDataCreate, db: Session = Depends(get_db)):
    obj = AdminData(name=payload.name, value=payload.value)
    db.add(obj)
    db.commit()
    db.refresh(obj)
    return obj


@router.put("/{admin_id}")
def update_admin_data(
    admin_id: int, payload: AdminDataUpdate, db: Session = Depends(get_db)
):
    fields = payload.model_dump(exclude_unset=True)
    if not fields:
        raise HTTPException(status_code=400, detail="No valid fields")

    stmt = update(AdminData).where(AdminData.id == admin_id).values(**fields)
    res = db.execute(stmt)
    db.commit()
    if res.rowcount == 0:
        raise HTTPException(status_code=404, detail="AdminData not found")
    return {"updated": True}


@router.delete("/{admin_id}", status_code=204)
def delete_admin_data(admin_id: int, db: Session = Depends(get_db)):
    stmt = delete(AdminData).where(AdminData.id == admin_id)
    res = db.execute(stmt)
    db.commit()
    if res.rowcount == 0:
        raise HTTPException(status_code=404, detail="AdminData not found")
