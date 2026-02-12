from typing import Generator

from sqlalchemy import create_engine
from sqlalchemy.orm import Session, declarative_base, sessionmaker

from app.config import DATABASE_URL

engine = create_engine(
    DATABASE_URL,
    echo=False,
    future=True,
    pool_pre_ping=True,
)

SessionLocal = sessionmaker(bind=engine, autoflush=False, autocommit=False, future=True)

Base = declarative_base()


def get_db() -> Generator[Session, None, None]:
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()
