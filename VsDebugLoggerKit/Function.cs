namespace VsDebugLoggerKit;

public delegate R Function<out R>();

public delegate R Function<out R, in T1>( T1 a1 );

public delegate R Function<out R, in T1, in T2>( T1 a1, T2 a2 );

public delegate R Function<out R, in T1, in T2, in T3>( T1 a1, T2 a2, T3 a3 );
