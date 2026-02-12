# admin_back.py
from fastapi import FastAPI, Request, Body
from fastapi.responses import JSONResponse
from fastapi.middleware.cors import CORSMiddleware
from typing import Optional, Dict, Any
from sqlalchemy import create_engine, String, Text, Integer, update, delete, select
from sqlalchemy.orm import declarative_base, sessionmaker, Mapped, mapped_column

# ---------------- DB setup ----------------
DATABASE_URL = "mysql+pymysql://root:141722@localhost:3306/farm_game?charset=utf8mb4"
engine = create_engine(DATABASE_URL, echo=False, future=True, pool_pre_ping=True)
SessionLocal = sessionmaker(bind=engine, autoflush=False, autocommit=False, future=True)
Base = declarative_base()

# ---------------- ORM ----------------
class AdminData(Base):
    __tablename__ = "admindata"
    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    name: Mapped[str] = mapped_column(String(255), nullable=False)
    value: Mapped[str] = mapped_column(Text, nullable=False)

Base.metadata.create_all(engine)

# ---------------- FastAPI ----------------
app = FastAPI(title="AdminData REST (PHP parity)", version="1.0.0")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # как в PHP
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# ---- uniform error helpers (как в PHP) ----
def json_error(status: int, msg: str):
    return JSONResponse(status_code=status, content={"error": msg})

NOT_FOUND_MSG = "AdminData not found"

# ---------------- Endpoints ----------------
@app.get("/admindata")
def get_all_admin_data():
    with SessionLocal() as db:
        rows = db.execute(select(AdminData)).scalars().all()
        return [{"id": r.id, "name": r.name, "value": r.value} for r in rows]

@app.get("/admindata/{admin_id}")
def get_admin_data(admin_id: int):
    with SessionLocal() as db:
        obj = db.get(AdminData, admin_id)
        if not obj:
            return json_error(404, NOT_FOUND_MSG)
        return {"id": obj.id, "name": obj.name, "value": obj.value}

@app.post("/admindata", status_code=201)
def create_admin_data(payload: Dict[str, Any] = Body(default=None)):
    if payload is None:
        return json_error(400, "name and value required")
    name = payload.get("name")
    value = payload.get("value")
    if name is None or value is None:
        return json_error(400, "name and value required")

    with SessionLocal() as db:
        obj = AdminData(name=name, value=value)
        db.add(obj)
        db.commit()
        db.refresh(obj)
        return {"id": obj.id, "name": obj.name, "value": obj.value}

@app.put("/admindata/{admin_id}")
def update_admin_data(admin_id: int, payload: Optional[Dict[str, Any]] = Body(default=None)):
    if not payload:
        return json_error(400, "No fields provided")

    # разрешены только эти поля
    fields = {}
    if "name" in payload:
        fields["name"] = payload["name"]
    if "value" in payload:
        fields["value"] = payload["value"]

    if not fields:
        return json_error(400, "No valid fields")

    with SessionLocal() as db:
        stmt = (
            update(AdminData)
            .where(AdminData.id == admin_id)
            .values(**fields)
        )
        res = db.execute(stmt)
        db.commit()
        # PHP: if affected_rows === 0 -> 404 (даже если id существует, но значения те же)
        if res.rowcount == 0:
            return json_error(404, NOT_FOUND_MSG)
        return {"updated": True}

@app.delete("/admindata/{admin_id}")
def delete_admin_data(admin_id: int):
    with SessionLocal() as db:
        stmt = delete(AdminData).where(AdminData.id == admin_id)
        res = db.execute(stmt)
        db.commit()
        if res.rowcount == 0:
            return json_error(404, NOT_FOUND_MSG)
        return JSONResponse(status_code=204, content=None)

# ---- опционально: единый обработчик 405 (как у PHP fallback) ----
@app.middleware("http")
async def method_not_allowed_passthrough(request: Request, call_next):
    # FastAPI сам отдаёт 405 на несоответствующие методы.
    # Здесь просто приводим content-type к JSON всегда.
    response = await call_next(request)
    if response.status_code == 405:
        return json_error(405, "Method Not Allowed")
    return response

# Запуск:
# uvicorn admin_back:app --port 8009 --reload
