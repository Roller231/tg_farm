from decimal import Decimal
from typing import Any, Dict, List, Optional

from fastapi import APIRouter, Depends, HTTPException, Path, Query
from sqlalchemy.orm import Session

from app.database import get_db
from app.models.product import Product
from app.schemas.product import (
    ProductCreate,
    ProductOut,
    ProductUpdate,
    SingleFieldUpdate,
)

router = APIRouter(prefix="/products", tags=["Products"])

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


def _get_product_or_404(db: Session, prod_id: int) -> Product:
    prod = db.get(Product, prod_id)
    if not prod:
        raise HTTPException(status_code=404, detail="Product not found")
    return prod


@router.post("", response_model=ProductOut, status_code=201)
def create_product(payload: ProductCreate, db: Session = Depends(get_db)):
    prod = Product(**payload.model_dump())
    db.add(prod)
    db.commit()
    db.refresh(prod)
    return prod


@router.get("", response_model=List[ProductOut])
def get_all_products(
    type: Optional[str] = Query(default=None, description="Filter by type"),
    db: Session = Depends(get_db),
):
    q = db.query(Product)
    if type is not None:
        q = q.filter(Product.type == type)
    return q.all()


@router.get("/by-type/{ptype}", response_model=List[ProductOut])
def get_products_by_type(
    ptype: str = Path(..., description="Product type. Use _empty for empty type"),
    db: Session = Depends(get_db),
):
    if ptype == "_empty":
        return db.query(Product).filter(Product.type == "").all()
    return db.query(Product).filter(Product.type == ptype).all()


@router.get("/{prod_id}", response_model=ProductOut)
def get_product(prod_id: int = Path(..., ge=1), db: Session = Depends(get_db)):
    return _get_product_or_404(db, prod_id)


@router.delete("/{prod_id}", status_code=204)
def delete_product(prod_id: int = Path(..., ge=1), db: Session = Depends(get_db)):
    prod = _get_product_or_404(db, prod_id)
    db.delete(prod)
    db.commit()


@router.put("/{prod_id}", response_model=ProductOut)
def update_product_full(
    prod_id: int = Path(..., ge=1),
    payload: ProductUpdate = ...,
    db: Session = Depends(get_db),
):
    if not payload.any_set():
        raise HTTPException(status_code=400, detail="No fields provided")
    prod = _get_product_or_404(db, prod_id)
    for field, value in payload.model_dump(exclude_unset=True).items():
        setattr(prod, field, FIELD_CASTERS[field](value))
    db.commit()
    db.refresh(prod)
    return prod


@router.patch("/{prod_id}", response_model=ProductOut)
def update_single_field(
    prod_id: int = Path(..., ge=1),
    update: SingleFieldUpdate = ...,
    db: Session = Depends(get_db),
):
    prod = _get_product_or_404(db, prod_id)
    try:
        cast_value = FIELD_CASTERS[update.field](update.value)
    except Exception:
        raise HTTPException(
            status_code=400, detail=f"Invalid value for field '{update.field}'"
        )
    setattr(prod, update.field, cast_value)
    db.commit()
    db.refresh(prod)
    return prod
