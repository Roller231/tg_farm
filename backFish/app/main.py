from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.database import Base, engine

# Import all models so Base.metadata knows about every table
import app.models  # noqa: F401

from app.routers import admin, level_rewards, products, tasks, transactions, users, withdraw

# Create tables that don't yet exist (won't alter existing ones)
Base.metadata.create_all(bind=engine)

app = FastAPI(
    title="Farm Game API",
    description="Unified backend for Farm Game — admin, users, products, rewards, withdrawals.",
    version="2.0.0",
    docs_url="/api/docs",
    redoc_url=None,
    openapi_url="/api/openapi.json",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# -------------------- Routers --------------------
app.include_router(admin.router, prefix="/api")
app.include_router(users.router, prefix="/api")
app.include_router(products.router, prefix="/api")
app.include_router(level_rewards.router, prefix="/api")
app.include_router(withdraw.router, prefix="/api")
app.include_router(tasks.router, prefix="/api")
app.include_router(transactions.router, prefix="/api")


@app.get("/", tags=["Health"])
def health_check():
    return {"status": "ok", "message": "Farm Game API is running"}
