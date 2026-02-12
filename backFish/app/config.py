import os

DATABASE_URL = os.getenv(
    "DATABASE_URL",
    "mysql+pymysql://root:141722@localhost:3306/farm_game?charset=utf8mb4",
)
