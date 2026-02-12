# Farm Game API

Unified backend for the Farm Game — combines admin, users, products, level rewards, and withdraw services into a single FastAPI application.

## Quick Start

```bash
# Install dependencies
pip install -r requirements.txt

# Run the API server
uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload

# Run the Telegram bot (separate process)
python bot.py
```

## Swagger UI

Open [http://localhost:8000/docs](http://localhost:8000/docs) after starting the server.

## Configuration

Set the `DATABASE_URL` environment variable to override the default MySQL connection:

```
DATABASE_URL=mysql+pymysql://user:pass@host:3306/farm_game?charset=utf8mb4
```

## API Endpoints

| Group          | Method   | Path                                  |
|----------------|----------|---------------------------------------|
| **Admin Data** | GET      | `/admindata`                          |
|                | GET      | `/admindata/{id}`                     |
|                | POST     | `/admindata`                          |
|                | PUT      | `/admindata/{id}`                     |
|                | DELETE   | `/admindata/{id}`                     |
| **Users**      | GET      | `/users`                              |
|                | GET      | `/users/{user_id}`                    |
|                | POST     | `/users`                              |
|                | PUT      | `/users/{user_id}`                    |
|                | PATCH    | `/users/{user_id}`                    |
|                | DELETE   | `/users/{user_id}`                    |
|                | GET      | `/users/{user_id}/houses`             |
|                | PUT      | `/users/{user_id}/houses`             |
|                | PATCH    | `/users/{user_id}/houses`             |
|                | POST     | `/users/{user_id}/houses/payout`      |
| **Products**   | GET      | `/products`                           |
|                | GET      | `/products/{id}`                      |
|                | GET      | `/products/by-type/{ptype}`           |
|                | POST     | `/products`                           |
|                | PUT      | `/products/{id}`                      |
|                | PATCH    | `/products/{id}`                      |
|                | DELETE   | `/products/{id}`                      |
| **Rewards**    | GET      | `/rewards`                            |
|                | GET      | `/rewards/{id}`                       |
|                | POST     | `/rewards`                            |
|                | DELETE   | `/rewards/{id}`                       |
| **Withdraw**   | POST     | `/withdraw`                           |
| **Health**     | GET      | `/`                                   |

## Project Structure

```
backFish/
├── app/
│   ├── main.py            # FastAPI application entry point
│   ├── config.py          # Environment-based configuration
│   ├── database.py        # SQLAlchemy engine, session, Base
│   ├── models/            # ORM models
│   │   ├── admin.py
│   │   ├── user.py
│   │   ├── product.py
│   │   ├── level_reward.py
│   │   └── withdraw.py
│   ├── schemas/           # Pydantic request/response schemas
│   │   ├── admin.py
│   │   ├── user.py
│   │   ├── product.py
│   │   ├── level_reward.py
│   │   └── withdraw.py
│   └── routers/           # API route handlers
│       ├── admin.py
│       ├── users.py
│       ├── products.py
│       ├── level_rewards.py
│       └── withdraw.py
├── bot.py                 # Telegram bot (separate process)
├── requirements.txt
└── README.md
```
