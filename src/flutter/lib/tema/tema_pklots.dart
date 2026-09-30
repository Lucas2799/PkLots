import 'package:flutter/material.dart';

import 'cores.dart';

class TemaPkLots {
  static ThemeData get claro {
    final esquema = ColorScheme.fromSeed(
      seedColor: CoresPkLots.azulMarinho,
      primary: CoresPkLots.azulMarinho,
      onPrimary: Colors.white,
      secondary: CoresPkLots.ambar,
      onSecondary: CoresPkLots.azulMarinho,
      surface: CoresPkLots.superficie,
      error: CoresPkLots.erro,
    );

    final bordaCampo = OutlineInputBorder(
      borderRadius: BorderRadius.circular(10),
      borderSide: const BorderSide(color: CoresPkLots.borda),
    );

    return ThemeData(
      useMaterial3: true,
      colorScheme: esquema,
      scaffoldBackgroundColor: CoresPkLots.fundo,
      appBarTheme: const AppBarTheme(
        backgroundColor: CoresPkLots.superficie,
        foregroundColor: CoresPkLots.azulMarinho,
        elevation: 0,
        scrolledUnderElevation: 1,
        centerTitle: false,
      ),
      cardTheme: CardThemeData(
        color: CoresPkLots.superficie,
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(14),
          side: const BorderSide(color: CoresPkLots.borda),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: CoresPkLots.superficie,
        border: bordaCampo,
        enabledBorder: bordaCampo,
        focusedBorder: bordaCampo.copyWith(
          borderSide: const BorderSide(color: CoresPkLots.azulMarinho, width: 2),
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: CoresPkLots.azulMarinho,
          foregroundColor: Colors.white,
          minimumSize: const Size.fromHeight(48),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
          textStyle: const TextStyle(fontWeight: FontWeight.w600, fontSize: 15),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: CoresPkLots.azulMarinho,
          minimumSize: const Size.fromHeight(48),
          side: const BorderSide(color: CoresPkLots.azulMarinho),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
        ),
      ),
      snackBarTheme: const SnackBarThemeData(behavior: SnackBarBehavior.floating),
    );
  }
}
