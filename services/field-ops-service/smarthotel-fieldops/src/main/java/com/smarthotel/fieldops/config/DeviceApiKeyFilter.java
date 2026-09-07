package com.smarthotel.fieldops.config;

import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;

import java.io.IOException;
import java.util.List;

@Component
public class DeviceApiKeyFilter extends OncePerRequestFilter {

    public static final String DEVICE_API_KEY_HEADER = "X-Device-Api-Key";

    @Value("${biometrics.device-api-key:smarthotel_device_secret_2026}")
    private String configuredApiKey;

    @Override
    protected void doFilterInternal(HttpServletRequest request, HttpServletResponse response, FilterChain filterChain)
            throws ServletException, IOException {

        String path = request.getRequestURI();

        // Guard the biometric punch webhook with Device API Key authentication
        if (path.startsWith("/api/v1/attendance/punch")) {
            String providedApiKey = request.getHeader(DEVICE_API_KEY_HEADER);

            if (providedApiKey == null || !providedApiKey.equals(configuredApiKey)) {
                response.setStatus(HttpServletResponse.SC_UNAUTHORIZED);
                response.setContentType("application/json");
                response.getWriter().write("{\"error\": \"Unauthorized\", \"message\": \"Invalid or missing device API key.\"}");
                return;
            }

            // Set authentication for the device
            var auth = new UsernamePasswordAuthenticationToken(
                    "BiometricPunchDevice",
                    null,
                    List.of(new SimpleGrantedAuthority("ROLE_DEVICE"))
            );
            SecurityContextHolder.getContext().setAuthentication(auth);
        }

        filterChain.doFilter(request, response);
    }
}
