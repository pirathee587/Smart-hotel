import { useCallback,useEffect,useRef } from "react";

/**
 * Reusable hook to manage request cancellation via AbortController.
 * - getSignal(): aborts any prior in-flight controller and returns a fresh AbortSignal.
 * - Automatically aborts active in-flight request when the component unmounts.
 */
export function useAbortController() {
  const controllerRef = useRef<AbortController | null>(null);

  const getSignal = useCallback((): AbortSignal => {
    if (controllerRef.current) {
      controllerRef.current.abort();
    }
    const newController = new AbortController();
    controllerRef.current = newController;
    return newController.signal;
  }, []);

  useEffect(() => {
    return () => {
      if (controllerRef.current) {
        controllerRef.current.abort();
      }
    };
  }, []);

  return { getSignal };
}

export default useAbortController;
