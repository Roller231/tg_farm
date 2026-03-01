import json
from decimal import Decimal
from typing import Any, Dict, List

from fastapi import APIRouter, Body, Depends, HTTPException, Path, Query
from sqlalchemy.orm import Session

from app.database import get_db
from app.models.product import Product
from app.models.user import User
from app.schemas.user import (
    IdStr,
    SingleFieldUpdate,
    UserCreate,
    UserOut,
    UserUpdate,
)

router = APIRouter(prefix="/users", tags=["Users"])

FIELD_CASTERS: Dict[str, Any] = {
    "name": str,
    "firstName": lambda v: None if v in (None, "null") else str(v),
    "ton": float,
    "lvl_upgrade": float,
    "lvl": int,
    "coin": Decimal,
    "bezoz": Decimal,
    "ref_count": int,
    "refId": lambda v: None if v in (None, "null") else str(v),
    "isPremium": int,
    "blocked": int,
    "time_farm": str,
    "seed_count": str,
    "storage_count": str,
    "grid_count": int,
    "grid_state": str,
    "houses": str,
}


def _get_user_or_404(db: Session, user_id: str) -> User:
    user = db.get(User, user_id)
    if not user:
        raise HTTPException(status_code=404, detail="User not found")
    return user


def _empty_houses_json() -> str:
    return json.dumps({"items": []}, ensure_ascii=False)


# -------------------- CRUD --------------------

@router.post("", response_model=UserOut, status_code=201)
def create_user(payload: UserCreate, db: Session = Depends(get_db)):
    if db.get(User, payload.id):
        raise HTTPException(status_code=409, detail="User with this id already exists")
    user = User(**payload.model_dump())
    db.add(user)
    db.commit()
    db.refresh(user)
    return user


@router.get("", response_model=List[UserOut])
def get_all_users(db: Session = Depends(get_db)):
    return db.query(User).all()


@router.get("/{user_id}", response_model=UserOut)
def get_user(user_id: IdStr = Path(...), db: Session = Depends(get_db)):
    return _get_user_or_404(db, user_id)


@router.delete("/{user_id}", status_code=204)
def delete_user(user_id: IdStr = Path(...), db: Session = Depends(get_db)):
    user = _get_user_or_404(db, user_id)
    db.delete(user)
    db.commit()


@router.put("/{user_id}", response_model=UserOut)
def update_user_full(
    user_id: IdStr = Path(...),
    payload: UserUpdate = Body(...),
    db: Session = Depends(get_db),
):
    if not payload.any_set():
        raise HTTPException(status_code=400, detail="No fields provided")
    user = _get_user_or_404(db, user_id)
    for field, value in payload.model_dump(exclude_unset=True).items():
        setattr(user, field, FIELD_CASTERS[field](value))
    db.commit()
    db.refresh(user)
    return user


@router.patch("/{user_id}", response_model=UserOut)
def update_single_field(
    user_id: IdStr = Path(...),
    update: SingleFieldUpdate = Body(...),
    db: Session = Depends(get_db),
):
    user = _get_user_or_404(db, user_id)
    try:
        cast_value = FIELD_CASTERS[update.field](update.value)
    except Exception:
        raise HTTPException(
            status_code=400, detail=f"Invalid value for field '{update.field}'"
        )
    setattr(user, update.field, cast_value)
    db.commit()
    db.refresh(user)
    return user


# -------------------- Houses --------------------

@router.get("/{user_id}/houses")
def get_houses(user_id: IdStr = Path(...), db: Session = Depends(get_db)):
    u = _get_user_or_404(db, user_id)
    houses = u.houses if (u.houses and u.houses.strip()) else _empty_houses_json()
    return {"houses": houses}


@router.put("/{user_id}/houses")
def put_houses(
    user_id: IdStr = Path(...),
    payload: Dict[str, Any] = Body(...),
    db: Session = Depends(get_db),
):
    u = _get_user_or_404(db, user_id)
    if "items" not in payload or not isinstance(payload["items"], list):
        raise HTTPException(status_code=400, detail="houses must have 'items' array")
    u.houses = json.dumps(payload, ensure_ascii=False)
    db.commit()
    db.refresh(u)
    return {"updated": True, "houses": u.houses}


@router.patch("/{user_id}/houses")
def patch_house(
    user_id: IdStr = Path(...),
    payload: Dict[str, Any] = Body(...),
    db: Session = Depends(get_db),
):
    u = _get_user_or_404(db, user_id)
    raw = u.houses.strip() if (u.houses and u.houses.strip()) else _empty_houses_json()
    try:
        data = json.loads(raw)
    except Exception:
        data = {"items": []}
    if "items" not in data or not isinstance(data["items"], list):
        data = {"items": []}

    if "id" not in payload:
        raise HTTPException(status_code=400, detail="house.id required")
    hid = int(payload["id"])

    found = False
    for i, h in enumerate(data["items"]):
        if int(h.get("id", -1)) == hid:
            data["items"][i] = {**h, **payload}
            found = True
            break
    if not found:
        data["items"].append(payload)

    u.houses = json.dumps(data, ensure_ascii=False)
    db.commit()
    db.refresh(u)
    return {"updated": True, "houses": u.houses}


@router.post("/{user_id}/houses/payout")
def house_payout(
    user_id: IdStr = Path(...),
    house_id: int = Query(..., ge=1),
    product_id: int = Query(..., ge=1),
    db: Session = Depends(get_db),
):
    u = _get_user_or_404(db, user_id)

    raw = u.houses.strip() if (u.houses and u.houses.strip()) else _empty_houses_json()
    try:
        data = json.loads(raw)
    except Exception:
        data = {"items": []}

    active_ok = False
    for h in data.get("items", []):
        if int(h.get("id", -1)) == int(house_id) and bool(h.get("active", False)):
            active_ok = True
            break
    if not active_ok:
        raise HTTPException(status_code=400, detail="house not active or not found")

    p = db.get(Product, product_id)
    if not p:
        raise HTTPException(status_code=404, detail="product not found")

    u.ton = float(u.ton) + float(p.sell_price)
    db.commit()
    db.refresh(u)
    return {"ton": u.ton}
