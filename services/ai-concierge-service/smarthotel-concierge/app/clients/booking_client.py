import logging
from typing import Optional
import grpc
from app.config import settings
from app.models.schemas import VerifiedStayContext
from app.grpc.booking_pb2 import ActiveStayRequest
from app.grpc.booking_pb2_grpc import BookingGrpcStub

logger = logging.getLogger(__name__)


class BookingClient:
    def __init__(self, grpc_host: Optional[str] = None, grpc_port: Optional[int] = None):
        self.grpc_host = grpc_host or settings.BOOKING_GRPC_HOST
        self.grpc_port = grpc_port or settings.BOOKING_GRPC_PORT
        self.target = f"{self.grpc_host}:{self.grpc_port}"

    async def get_active_stay(self, customer_id: str, auth_token: Optional[str] = None) -> VerifiedStayContext:
        """
        Calls Booking Service via cross-language gRPC (Python -> .NET Kestrel HTTP/2)
        to retrieve verified active-stay context.
        NEVER trusts client-supplied room claims.
        """
        try:
            async with grpc.aio.insecure_channel(self.target) as channel:
                stub = BookingGrpcStub(channel)
                req = ActiveStayRequest(customer_id=customer_id)
                response = await stub.GetActiveStay(req, timeout=4.0)

                if response.has_active_stay:
                    logger.info(
                        f"gRPC GetActiveStay resolved active stay for customer {customer_id}: "
                        f"room {response.room_number}, ref {response.booking_reference}"
                    )
                    return VerifiedStayContext(
                        booking_id=response.booking_reference or "",
                        booking_reference=response.booking_reference,
                        customer_id=customer_id,
                        room_id="",
                        room_number=response.room_number or "101",
                        room_type_id="",
                        check_in_date="",
                        check_out_date=response.check_out_date,
                        guest_count=1,
                        is_active=True
                    )
                else:
                    logger.info(f"gRPC GetActiveStay: no active stay found for customer {customer_id}")
                    return VerifiedStayContext(customer_id=customer_id, is_active=False)

        except grpc.RpcError as rpc_err:
            logger.warning(f"gRPC call GetActiveStay failed with code {rpc_err.code()}: {rpc_err.details()}")
            return VerifiedStayContext(customer_id=customer_id, is_active=False)
        except Exception as ex:
            logger.warning(f"Unexpected error in gRPC GetActiveStay: {ex}")
            return VerifiedStayContext(customer_id=customer_id, is_active=False)


booking_client = BookingClient()
