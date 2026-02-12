from fastapi import APIRouter, Depends
from sqlalchemy.orm import Session

from app.database import get_db
from app.models.withdraw import WithdrawRequest
from app.schemas.withdraw import WithdrawCreate, WithdrawOut

router = APIRouter(prefix="/withdraw", tags=["Withdraw"])


@router.post("", response_model=WithdrawOut, status_code=201)
def create_withdraw(payload: WithdrawCreate, db: Session = Depends(get_db)):
    req = WithdrawRequest(
        ton_address=payload.ton_address,
        amount=payload.amount,
        memo=payload.memo,
    )
    db.add(req)
    db.commit()
    db.refresh(req)
    return req
