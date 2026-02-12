# users_backend.py
from fastapi import FastAPI, HTTPException, Path, Body, Query
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field, constr
from typing import Optional, Literal, Dict, Any, List
from decimal import Decimal
from sqlalchemy import create_engine, String, Integer, Numeric, Float, Text
from sqlalchemy.orm import declarative_base, sessionmaker, Mapped, mapped_column
import os, json

# -------------------- DB setup --------------------
DATABASE_URL = os.getenv("USERS_DATABASE_URL", "mysql+pymysql://root:141722@localhost:3306/farm_game?charset=utf8mb4")

is_sqlite = DATABASE_URL.startswith("sqlite:")
engine = create_engine(
    DATABASE_URL, echo=False, future=True, pool_pre_ping=True,
    connect_args={"check_same_thread": False} if is_sqlite else {}
)
SessionLocal = sessionmaker(bind=engine, autoflush=False, autocommit=False, future=True)
Base = declarative_base()

# ---------------- ORM ----------------
class User(Base):
    __tablename__ = "users"

    id: Mapped[str] = mapped_column(String(100), primary_key=True)
    name: Mapped[str] = mapped_column(String(100), nullable=False)
    firstName: Mapped[Optional[str]] = mapped_column(String(100), nullable=True, default=None)

    ton: Mapped[float] = mapped_column(Float, nullable=False, default=0.0)
    lvl_upgrade: Mapped[float] = mapped_column(Float, nullable=False, default=0.0)
    lvl: Mapped[int] = mapped_column(Integer, nullable=False, default=1)
    coin: Mapped[Decimal] = mapped_column(Numeric(12, 2), nullable=False, default=Decimal("100"))
    bezoz: Mapped[Decimal] = mapped_column(Numeric(12, 2), nullable=False, default=Decimal("10"))
    ref_count: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    refId: Mapped[Optional[str]] = mapped_column(String(255), nullable=True, default=None)

    # NEW: премиум-статус
    isPremium: Mapped[int] = mapped_column(Integer, nullable=False, default=0)

    time_farm: Mapped[str] = mapped_column(Text, nullable=False, default="")
    seed_count: Mapped[str] = mapped_column(Text, nullable=False, default="")
    storage_count: Mapped[str] = mapped_column(Text, nullable=False, default="")
    grid_count: Mapped[int] = mapped_column(Integer, nullable=False, default=3)
    grid_state: Mapped[str] = mapped_column(Text, nullable=False, default="")

    houses: Mapped[str] = mapped_column(Text, nullable=False, default="")  # '{"items":[{...}]}'

# Важно: create_all не обновит таблицу, если она уже создана
Base.metadata.create_all(engine)

# ---------------- Schemas ----------------
IdStr = constr(strip_whitespace=True, min_length=1, max_length=100)
NameStr = constr(strip_whitespace=True, min_length=1, max_length=100)

class UserCreate(BaseModel):
    id: IdStr
    name: NameStr
    firstName: Optional[str] = None

    ton: float = 0
    lvl_upgrade: float = 0
    lvl: int = 1
    coin: Decimal = Field(default=Decimal("100"))
    bezoz: Decimal = Field(default=Decimal("10"))
    ref_count: int = 0
    refId: Optional[str] = None

    isPremium: int = 0   # NEW

    time_farm: str = ""
    seed_count: str = ""
    storage_count: str = ""
    grid_count: int = 3
    grid_state: str = ""

    houses: str = ""

class UserUpdate(BaseModel):
    name: Optional[NameStr] = None
    firstName: Optional[str] = None

    ton: Optional[float] = None
    lvl_upgrade: Optional[float] = None
    lvl: Optional[int] = None
    coin: Optional[Decimal] = None
    bezoz: Optional[Decimal] = None
    ref_count: Optional[int] = None
    refId: Optional[str] = None

    isPremium: Optional[int] = None   # NEW

    time_farm: Optional[str] = None
    seed_count: Optional[str] = None
    storage_count: Optional[str] = None
    grid_count: Optional[int] = None
    grid_state: Optional[str] = None

    houses: Optional[str] = None

    def any_set(self) -> bool:
        return bool(self.model_dump(exclude_unset=True))

AllowedField = Literal[
    "name","firstName","ton","lvl_upgrade","lvl","coin","bezoz",
    "ref_count","refId","isPremium",
    "time_farm","seed_count","storage_count","grid_count","grid_state",
    "houses"
]

class SingleFieldUpdate(BaseModel):
    field: AllowedField
    value: Any

class UserOut(BaseModel):
    id: str
    name: str
    firstName: Optional[str] = None

    ton: float
    lvl_upgrade: float
    lvl: int
    coin: Decimal
    bezoz: Decimal
    ref_count: int
    refId: Optional[str] = None

    isPremium: int   # NEW

    time_farm: str
    seed_count: str
    storage_count: str
    grid_count: int
    grid_state: str

    houses: str

    class Config:
        from_attributes = True

# ---------------- Products model ----------------
from sqlalchemy import ForeignKey
class _Product(Base):
    __tablename__ = "products"
    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    sell_price: Mapped[Decimal] = mapped_column(Numeric(10, 2), nullable=False)

# ---------------- App ----------------
app = FastAPI(title="Users REST", version="1.2.0")
app.add_middleware(
    CORSMiddleware, allow_origins=["*"], allow_credentials=True,
    allow_methods=["*"], allow_headers=["*"]
)

FIELD_CASTERS: Dict[str, Any] = {
    "name": str,
    "firstName": (lambda v: None if v in (None, "null") else str(v)),
    "ton": float, "lvl_upgrade": float, "lvl": int,
    "coin": Decimal, "bezoz": Decimal, "ref_count": int,
    "refId": (lambda v: None if v in (None, "null") else str(v)),
    "isPremium": int,
    "time_farm": str, "seed_count": str, "storage_count": str,
    "grid_count": int, "grid_state": str,
    "houses": str,
}

def get_user_or_404(db, user_id: str) -> User:
    user = db.get(User, user_id)
    if not user:
        raise HTTPException(404, detail="User not found")
    return user

# ---------------- CRUD Users ----------------
@app.post("/users", response_model=UserOut, status_code=201)
def create_user(payload: UserCreate):
    with SessionLocal() as db:
        if db.get(User, payload.id):
            raise HTTPException(409, detail="User with this id already exists")
        user = User(**payload.model_dump())
        db.add(user)
        db.commit()
        db.refresh(user)
        return user

@app.get("/users/{user_id}", response_model=UserOut)
def get_user(user_id: IdStr = Path(...)):
    with SessionLocal() as db:
        return get_user_or_404(db, user_id)

@app.delete("/users/{user_id}", status_code=204)
def delete_user(user_id: IdStr):
    with SessionLocal() as db:
        user = get_user_or_404(db, user_id)
        db.delete(user)
        db.commit()

@app.get("/users", response_model=List[UserOut])
def get_all_users():
    with SessionLocal() as db:
        users = db.query(User).all()
        return users

@app.put("/users/{user_id}", response_model=UserOut)
def update_user_full(user_id: IdStr, payload: UserUpdate = Body(...)):
    if not payload.any_set():
        raise HTTPException(400, detail="No fields provided")
    with SessionLocal() as db:
        user = get_user_or_404(db, user_id)
        for field, value in payload.model_dump(exclude_unset=True).items():
            setattr(user, field, FIELD_CASTERS[field](value))
        db.commit()
        db.refresh(user)
        return user

@app.patch("/users/{user_id}", response_model=UserOut)
def update_single_field(user_id: IdStr, update: SingleFieldUpdate):
    with SessionLocal() as db:
        user = get_user_or_404(db, user_id)
        try:
            cast_value = FIELD_CASTERS[update.field](update.value)
        except Exception:
            raise HTTPException(400, detail=f"Invalid value for field '{update.field}'")
        setattr(user, update.field, cast_value)
        db.commit()
        db.refresh(user)
        return user

# ---------------- Houses helpers & endpoints ----------------
def _empty_houses_json() -> str:
    return json.dumps({"items": []}, ensure_ascii=False)

@app.get("/users/{user_id}/houses")
def get_houses(user_id: IdStr):
    with SessionLocal() as db:
        u = get_user_or_404(db, user_id)
        houses = u.houses if (u.houses and u.houses.strip()) else _empty_houses_json()
        return {"houses": houses}

@app.put("/users/{user_id}/houses")
def put_houses(user_id: IdStr, payload: Dict[str, Any]):
    with SessionLocal() as db:
        u = get_user_or_404(db, user_id)
        if "items" not in payload or not isinstance(payload["items"], list):
            raise HTTPException(400, "houses must have 'items' array")
        u.houses = json.dumps(payload, ensure_ascii=False)
        db.commit(); db.refresh(u)
        return {"updated": True, "houses": u.houses}

@app.patch("/users/{user_id}/houses")
def patch_house(user_id: IdStr, payload: Dict[str, Any]):
    with SessionLocal() as db:
        u = get_user_or_404(db, user_id)
        raw = u.houses.strip() if (u.houses and u.houses.strip()) else _empty_houses_json()
        try:
            data = json.loads(raw)
        except Exception:
            data = {"items": []}
        if "items" not in data or not isinstance(data["items"], list):
            data = {"items": []}

        if "id" not in payload:
            raise HTTPException(400, "house.id required")
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
        db.commit(); db.refresh(u)
        return {"updated": True, "houses": u.houses}

@app.post("/users/{user_id}/houses/payout")
def house_payout(
    user_id: IdStr,
    house_id: int = Query(..., ge=1),
    product_id: int = Query(..., ge=1)
):
    with SessionLocal() as db:
        u = get_user_or_404(db, user_id)

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
            raise HTTPException(400, "house not active or not found")

        p = db.get(_Product, product_id)
        if not p:
            raise HTTPException(404, "product not found")

        u.ton = float(u.ton) + float(p.sell_price)
        db.commit(); db.refresh(u)
        return {"ton": u.ton}
