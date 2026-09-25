package com.smarthotel.fieldops.config;

import com.smarthotel.fieldops.domain.model.FnbTaxRule;
import com.smarthotel.fieldops.domain.model.MenuItem;
import com.smarthotel.fieldops.domain.repository.FnbTaxRuleRepository;
import com.smarthotel.fieldops.domain.repository.MenuItemRepository;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.boot.CommandLineRunner;
import org.springframework.stereotype.Component;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.List;

@Component
@RequiredArgsConstructor
@Slf4j
public class FnbDataSeeder implements CommandLineRunner {

    private final MenuItemRepository menuItemRepository;
    private final FnbTaxRuleRepository taxRuleRepository;

    @Override
    public void run(String... args) {
        seedMenuItems();
        seedTaxRules();
    }

    private void seedMenuItems() {
        if (menuItemRepository.count() > 0) {
            return;
        }

        log.info("Seeding initial Food & Beverage menu items...");

        List<MenuItem> items = List.of(
                // Mains
                MenuItem.builder()
                        .code("MAIN-KOTTU-01")
                        .name("Chicken Kottu Roti")
                        .description("Flaky shredded flatbread stir-fried with tender chicken, vegetables, egg, and spicy curry gravy.")
                        .category("Mains")
                        .price(new BigDecimal("1800.00"))
                        .currency("LKR")
                        .available(true)
                        .roomServiceEligible(true)
                        .build(),

                MenuItem.builder()
                        .code("MAIN-RICE-02")
                        .name("Seafood Fried Rice")
                        .description("Wok-tossed basmati rice with tiger prawns, calamari, fried egg, and homemade chili paste.")
                        .category("Mains")
                        .price(new BigDecimal("2200.00"))
                        .currency("LKR")
                        .available(true)
                        .roomServiceEligible(true)
                        .build(),

                MenuItem.builder()
                        .code("MAIN-CURRY-03")
                        .name("Traditional Sri Lankan Rice & Curry")
                        .description("Steamed fragrant rice served with chicken curry, dhal, pol sambol, and crispy papadam.")
                        .category("Mains")
                        .price(new BigDecimal("1600.00"))
                        .currency("LKR")
                        .available(true)
                        .roomServiceEligible(true)
                        .build(),

                MenuItem.builder()
                        .code("MAIN-SANDWICH-04")
                        .name("Smart Hotel Club Sandwich")
                        .description("Triple-decker toasted sandwich with roasted chicken breast, fried egg, cheese, and french fries.")
                        .category("Mains")
                        .price(new BigDecimal("1750.00"))
                        .currency("LKR")
                        .available(true)
                        .roomServiceEligible(true)
                        .build(),

                MenuItem.builder()
                        .code("MAIN-BURGER-05")
                        .name("Grilled Beef Burger")
                        .description("Juicy grilled beef patty with caramelized onions, cheddar cheese, and rustic potato wedges.")
                        .category("Mains")
                        .price(new BigDecimal("2600.00"))
                        .currency("LKR")
                        .available(true)
                        .roomServiceEligible(true)
                        .build(),

                // Starters
                MenuItem.builder()
                        .code("STARTER-HOPPER-01")
                        .name("Egg Hopper Trio")
                        .description("Three bowl-shaped crispy rice flour crepes with soft egg centers, served with tangy lunu miris.")
                        .category("Starters")
                        .price(new BigDecimal("950.00"))
                        .currency("LKR")
                        .available(true)
                        .roomServiceEligible(true)
                        .build(),

                MenuItem.builder()
                        .code("STARTER-ROLLS-02")
                        .name("Crispy Vegetable Spring Rolls")
                        .description("Golden fried rolls stuffed with spiced vegetables, served with sweet chili dipping sauce.")
                        .category("Starters")
                        .price(new BigDecimal("850.00"))
                        .currency("LKR")
                        .available(true)
                        .roomServiceEligible(true)
                        .build(),

                MenuItem.builder()
                        .code("STARTER-SOUP-03")
                        .name("Spiced Jaffna Crab Soup")
                        .description("Rich and aromatic blue swimmer crab soup infused with coriander and toasted cumin.")
                        .category("Starters")
                        .price(new BigDecimal("1200.00"))
                        .currency("LKR")
                        .available(true)
                        .roomServiceEligible(true)
                        .build(),

                // Beverages
                MenuItem.builder()
                        .code("BEV-TEA-01")
                        .name("Ceylon Spiced Milk Tea")
                        .description("Single-origin Ceylon black tea brewed with fresh milk, cardamom, and cinnamon.")
                        .category("Beverages")
                        .price(new BigDecimal("450.00"))
                        .currency("LKR")
                        .available(true)
                        .roomServiceEligible(true)
                        .build(),

                MenuItem.builder()
                        .code("BEV-THAMBILI-02")
                        .name("Fresh King Coconut (Thambili)")
                        .description("Naturally sweet, chilled organic king coconut water served straight from the shell.")
                        .category("Beverages")
                        .price(new BigDecimal("400.00"))
                        .currency("LKR")
                        .available(true)
                        .roomServiceEligible(true)
                        .build(),

                MenuItem.builder()
                        .code("BEV-JUICE-03")
                        .name("Papaya & Passion Fruit Cooler")
                        .description("Freshly squeezed tropical papaya blended with tart passion fruit nectar.")
                        .category("Beverages")
                        .price(new BigDecimal("700.00"))
                        .currency("LKR")
                        .available(true)
                        .roomServiceEligible(true)
                        .build(),

                // Desserts
                MenuItem.builder()
                        .code("DESSERT-WATALAPPAN-01")
                        .name("Traditional Watalappan")
                        .description("Steamed coconut milk custard sweetened with jaggery, scented with nutmeg and roasted cashews.")
                        .category("Desserts")
                        .price(new BigDecimal("800.00"))
                        .currency("LKR")
                        .available(true)
                        .roomServiceEligible(true)
                        .build(),

                MenuItem.builder()
                        .code("DESSERT-CURD-02")
                        .name("Buffalo Curd & Kithul Treacle")
                        .description("Creamy traditional clay-pot curd served with pure artisanal kithul palm treacle.")
                        .category("Desserts")
                        .price(new BigDecimal("650.00"))
                        .currency("LKR")
                        .available(true)
                        .roomServiceEligible(true)
                        .build()
        );

        menuItemRepository.saveAll(items);
        log.info("Successfully seeded {} menu items.", items.size());
    }

    private void seedTaxRules() {
        if (taxRuleRepository.count() > 0) {
            return;
        }

        log.info("Seeding baseline F&B tax rule...");

        FnbTaxRule defaultRule = FnbTaxRule.builder()
                .category("FoodAndBeverage")
                .taxRate(BigDecimal.ZERO)
                .effectiveFrom(Instant.EPOCH)
                .description("Baseline tax rule (0.00% until official rates configured)")
                .active(true)
                .build();

        taxRuleRepository.save(defaultRule);
    }
}
