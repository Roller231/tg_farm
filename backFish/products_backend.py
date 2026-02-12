# products_backend.py
from fastapi import FastAPI, HTTPException, Path, Query
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field, constr
from typing import Optional, Literal, Dict, Any, List
from decimal import Decimal
from sqlalchemy import create_engine, String, Integer, Numeric, Text, select
from sqlalchemy.orm import declarative_base, sessionmaker, Mapped, mapped_column
import os

# -------------------- DB setup --------------------
# Пример: "mysql+pymysql://user:pass@localhost:3306/farm_game?charset=utf8mb4"
DATABASE_URL = os.getenv("PRODUCTS_DATABASE_URL", "mysql+pymysql://root:141722@localhost:3306/farm_game?charset=utf8mb4")
is_sqlite = DATABASE_URL.startswith("sqlite:")

engine = create_engine(
    DATABASE_URL,
    echo=False,
    future=True,
    pool_pre_ping=True,
    connect_args={"check_same_thread": False} if is_sqlite else {}
)
SessionLocal = sessionmaker(bind=engine, autoflush=False, autocommit=False, future=True)
Base = declarative_base()

# -------------------- ORM модель: products --------------------
class Product(Base):
    __tablename__ = "products"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    name: Mapped[str] = mapped_column(String(100), nullable=False)
    type: Mapped[str] = mapped_column(String(50), nullable=False, default="")    # NEW
    price: Mapped[Decimal] = mapped_column(Numeric(10, 2), nullable=False)
    sell_price: Mapped[Decimal] = mapped_column(Numeric(10, 2), nullable=False)
    speed_price: Mapped[Decimal] = mapped_column(Numeric(10, 2), nullable=False)
    lvl_for_buy: Mapped[int] = mapped_column(Integer, nullable=False)
    time: Mapped[int] = mapped_column(Integer, nullable=False)
    exp: Mapped[Decimal] = mapped_column(Numeric(10, 4), nullable=False, default=Decimal("0"))  # точность до 4 знаков
    image_seed_link: Mapped[str] = mapped_column(Text, nullable=False)
    image_ready_link: Mapped[str] = mapped_column(Text, nullable=False)

# ВНИМАНИЕ: create_all НЕ добавит колонку в существующую таблицу автоматически!
Base.metadata.create_all(engine)

# -------------------- Pydantic схемы --------------------
NameStr = constr(strip_whitespace=True, min_length=1, max_length=100)

class ProductCreate(BaseModel):
    name: NameStr
    type: str = ""                                 # NEW
    price: Decimal
    sell_price: Decimal
    speed_price: Decimal
    lvl_for_buy: int
    time: int
    exp: Decimal = Field(default=Decimal("0"))     # NEW
    image_seed_link: str
    image_ready_link: str

class ProductUpdate(BaseModel):
    name: Optional[NameStr] = None
    type: Optional[str] = None                     # NEW
    price: Optional[Decimal] = None
    sell_price: Optional[Decimal] = None
    speed_price: Optional[Decimal] = None
    lvl_for_buy: Optional[int] = None
    time: Optional[int] = None
    exp: Optional[Decimal] = None                  # NEW
    image_seed_link: Optional[str] = None
    image_ready_link: Optional[str] = None

    def any_set(self) -> bool:
        return bool(self.model_dump(exclude_unset=True))

AllowedField = Literal[
    "name", "type", "price", "sell_price", "speed_price",
    "lvl_for_buy", "time", "exp",
    "image_seed_link", "image_ready_link"
]

class SingleFieldUpdate(BaseModel):
    field: AllowedField
    value: Any

class ProductOut(BaseModel):
    id: int
    name: str
    type: str
    price: Decimal
    sell_price: Decimal
    speed_price: Decimal
    lvl_for_buy: int
    time: int
    exp: Decimal
    image_seed_link: str
    image_ready_link: str

    class Config:
        from_attributes = True

# -------------------- App --------------------
app = FastAPI(title="Products REST", version="1.2.0")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"], allow_credentials=True,
    allow_methods=["*"], allow_headers=["*"]
)

FIELD_CASTERS: Dict[str, Any] = {
    "name": str,
    "type": str,
    "price": Decimal,
    "sell_price": Decimal,
    "speed_price": Decimal,
    "lvl_for_buy": int,
    "time": int,
    "exp": Decimal,
    "image_seed_link": str,
    "image_ready_link": str,
}

def get_product_or_404(db, prod_id: int) -> Product:
    prod = db.get(Product, prod_id)
    if not prod:
        raise HTTPException(404, detail="Product not found")
    return prod

# -------------------- Endpoints --------------------
@app.post("/products", response_model=ProductOut, status_code=201)
def create_product(payload: ProductCreate):
    with SessionLocal() as db:
        prod = Product(**payload.model_dump())
        db.add(prod)
        db.commit()
        db.refresh(prod)
        return prod

@app.get("/products", response_model=List[ProductOut])
def get_all_products(type: Optional[str] = Query(default=None, description="Фильтр по типу; для пустого типа передай '' или опусти параметр")):
    with SessionLocal() as db:
        q = db.query(Product)
        if type is not None:
            q = q.filter(Product.type == type)
        return q.all()

@app.get("/products/by-type/{ptype}", response_model=List[ProductOut])
def get_products_by_type(ptype: str = Path(..., description="Тип продукта. Для пустого типа используй _empty")):
    with SessionLocal() as db:
        if ptype == "_empty":
            return db.query(Product).filter(Product.type == "").all()
        return db.query(Product).filter(Product.type == ptype).all()

@app.get("/products/{prod_id}", response_model=ProductOut)
def get_product(prod_id: int = Path(..., ge=1)):
    with SessionLocal() as db:
        return get_product_or_404(db, prod_id)

@app.delete("/products/{prod_id}", status_code=204)
def delete_product(prod_id: int = Path(..., ge=1)):
    with SessionLocal() as db:
        prod = get_product_or_404(db, prod_id)
        db.delete(prod)
        db.commit()
        return

@app.put("/products/{prod_id}", response_model=ProductOut)
def update_product_full(prod_id: int, payload: ProductUpdate):
    if not payload.any_set():
        raise HTTPException(400, detail="No fields provided")
    with SessionLocal() as db:
        prod = get_product_or_404(db, prod_id)
        for field, value in payload.model_dump(exclude_unset=True).items():
            setattr(prod, field, FIELD_CASTERS[field](value))
        db.commit()
        db.refresh(prod)
        return prod

@app.patch("/products/{prod_id}", response_model=ProductOut)
def update_single_field(prod_id: int, update: SingleFieldUpdate):
    with SessionLocal() as db:
        prod = get_product_or_404(db, prod_id)
        field = update.field
        try:
            cast_value = FIELD_CASTERS[field](update.value)
        except Exception:
            raise HTTPException(400, detail=f"Invalid value for field '{field}'")
        setattr(prod, field, cast_value)
        db.commit()
        db.refresh(prod)
        return prod

# Запуск:
# uvicorn products_backend:app --port 8008 --reload
