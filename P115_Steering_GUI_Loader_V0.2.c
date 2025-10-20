/********************************************************************/
/*                                                                  */
/*  NEXT SOLUTION TECHNOLOGIES 2025	 								*/
/*																	*/
/*	Steering P115_GUI_LOADER              					        */
/*	                                                                */
/*  Per l'elenco delle versioni fare riferimento al documento       */
/*  P115_GUI_LOADER_REVISION_HISTORY                         */
/*                                                                  */
/********************************************************************/



#include <p30F5013.h>
#include "eeprom_rw.h"
#include <math.h>
#include "ConfigurationBits_5013.h"



#define FIRMWAREVERSION           2 // 1 Significa 0.1 

#define MAXCHANNELS				  1
#define MAXCANID				  2
#define MAXCONTATORECAN_MESSAGE1 20 // Periodo Trasmissione Primo Messaggio CAN 20Hz
#define MAXCONTATORECAN_MESSAGE2 20 // Periodo Trasmissione Secondo Messaggio CAN 20Hz


#define TRUE					 1
#define FALSE				     0

#define ANALOGCHANNEL_2 		 2 // Canale Analogico 2 su P105 TEMPERATURA PCB
#define ANALOGCHANNEL_3          3 // Canale Analogico 3 su P105 PADDLE_1
#define ANALOGCHANNEL_4          4 // Canale Analogico 4 su P105 PADDLE_2
#define ANALOGCHANNEL_5          5 // Canale Analogico 5 su P105 BATTERY
#define ANALOGCHANNEL_6          6 // Canale Analogico 6 su P105 BOOTLOADER
#define ANALOGCHANNEL_7          7 // Canale Analogico 7 su P105 OPTION
#define ANALOGCHANNEL_8          8 // Canale Analogico 8 su P105 ROTARY_1
#define ANALOGCHANNEL_9          9 // Canale Analogico 9 su P105 ROTARY_2
#define ANALOGCHANNEL_10        10 // Canale Analogico 10
#define ANALOGCHANNEL_11        11 // Canale Analogico 11
#define ANALOGCHANNEL_12        12 // Canale Analogico 12
#define ANALOGCHANNEL_13        13 // Canale Analogico 13
#define ANALOGCHANNEL_14        14 // Canale Analogico 14



#define MAXUARTBUFFERDIM       255 // Massima dimensione del buffer di ricezione della seriale
#define MAXRAMBLOCKDIM         240 // Massima dimensione del blocco di dati da scrivere in RAM

// ---------------------DA ECU-------------------------- //

#define MAX_CAN_TIME_OUT         500 // Se non ricevo il messaggio CAN per gestire la rain light per un secondo la faccio partire con valori standard.


unsigned int CAN_IN_ID[ MAXCANID ] = { 0x500 };

#define CAN_IN_TRIGGER_ID      0x500 // CAN ID che triggera la risposta sul 0x600
unsigned char AnswerNeeded = FALSE;
unsigned char ActionNeeded = FALSE;

// ---------------------- DA VOLANTE ---------------------//

#define CAN_OUT_ID_1		     0x600 // Byte 0 Button Status 1 
#define CAN_OUT_ID_2             0x604 // Steering PADDLE Status
//#define CAN_OUT_ID_3             0x236 // Steering Status Info
//#define CAN_OUT_ID_4             0x237 // Byte 0 Steering Page Number
//#define CAN_OUT_ID_5             0x238 // Byte 0 Button Status 2

//#define CAN2_OUT_ID_1		     0x234 // Non Usato

//------------------------------------- Definizione Variabili Globali -----------------------------------

unsigned int CAN_Time_Out;


//---------------------------------------------------------------------------------------------------------------------------------------------------------------//



unsigned char IDCAN_ByteL, IDCAN_ByteH, Register_CAN_ID_L, Register_CAN_ID_H;
unsigned char Can_RX_Buffer [8] = { 0,0,0,0,0,0,0,0 };
unsigned char Can_Tx_Buffer [8] = { 0,0,0,0,0,0,0,0 };

unsigned char Can2_RX_Buffer [8] = { 0,0,0,0,0,0,0,0 };
unsigned char Can2_Tx_Buffer [8] = { 0,0,0,0,0,0,0,0 };

unsigned int CANMatrix[ MAXCANID ][ 8 ];
unsigned int CAN2Matrix[ MAXCANID ][ 8 ];

unsigned int Channels[ MAXCHANNELS ];
unsigned int MessageID,MessageID2;
unsigned char ReceivedByte,CANRow,CAN2Row;

unsigned char NoCAN =1;
int ContatoreCAN_Message1=0, ContatoreCAN_Message2 = ( MAXCONTATORECAN_MESSAGE2 / 5 );

unsigned char BufferUART1 [ MAXUARTBUFFERDIM ];
unsigned int BufferUART1Count = 0;
unsigned char DataUART1 = 0;
unsigned char CommandSend = 0x00; // Tipo di comando inviato alla seriale serve per capire che risposta aspettarsi
unsigned char BloccoRAM [ MAXRAMBLOCKDIM ];
unsigned char Offset_RAM_Block;
unsigned int Indirizzo_RAM = 0, Lunghezza_Blocco = 0, Indirizzo_FLASH = 0;
unsigned char RAM_to_Flash_Copy_Status = 0;

unsigned long DataMessageNumber = 0;

unsigned char LiveCounter = 0;
unsigned int i = 0;
unsigned char contatore_automa = 0;

int Delay;
int Attendi;
int Atteso=0;

int PR2M = 0x200;

void Wait( int Hundreds );
void SettaModalitaAggiornamento( void ); // Mette lo schermo DWIN in modalità di programmazione.
void ScriviBloccoRAM( unsigned int Indirizzo_RAM, unsigned char Blocco[ MAXRAMBLOCKDIM ], unsigned int Lunghezza_Blocco ); // Scrive Blocco Dati nella RAM dello schermo DWin
void CopiaRAMinFLASH( unsigned int Indirizzo_FLASH );
void InterrogaSchermo( void ); // Dopo aver mandato il comando per copiare la Ram in Flash con questa funzione interrogo lo schermo per capire se ha finito l'operazione
void ResettaSchermo( void ); // Resetta lo schermo
void ResetBufferUART1 ( void ); // Resetta a zero il buffer della seriale UART1

//---------------------------------------------------------------------


int main(void)
{
    
 
 // Configurazione Porte Digitali / Analogiche
 
  TRISD = 0xFCFF; // Tutte le linee della porta D sono Input tranne quella che pilota il la bobina del relais RADIO e i led pulsanti 1111 1100 1111 1111
  PORTD = 0x0000;

  TRISB = 0xFFFF; // Tutte le linee della porta B sono Input

 // Configurazione Port B - Output Digitali - 
   
  ADPCFGbits.PCFG0 = 1; // PGC Programmazione
  ADPCFGbits.PCFG1 = 1; // PGD Programmazione
  ADPCFGbits.PCFG2 = 0; // Pin AN2 usato come ingresso analogico 
  ADPCFGbits.PCFG3 = 0; // Pin AN3 usato come ingresso analogico 
  ADPCFGbits.PCFG4 = 0; // Pin AN4 usato come ingresso analogico 
  ADPCFGbits.PCFG5 = 0; // Pin AN5 usato come ingresso analogico 
  ADPCFGbits.PCFG6 = 0; // Pin AN6 usato come ingresso analogico 
  ADPCFGbits.PCFG7 = 0; // Pin AN7 usato come ingresso analogico 
  ADPCFGbits.PCFG8 = 0; // Pin AN8 usato come ingresso analogico 
  ADPCFGbits.PCFG9 = 0; // Pin AN9 usato come ingresso analogico 
  ADPCFGbits.PCFG10 = 0; // Pin AN10 usato come ingresso analogico 
  ADPCFGbits.PCFG11 = 0; // Pin AN11 usato come ingresso analogico 
  ADPCFGbits.PCFG12 = 0; // Pin AN12 usato come ingresso analogico 
  ADPCFGbits.PCFG13 = 0; // Pin AN13 usato come ingresso analogico 
  ADPCFGbits.PCFG14 = 0; // Pin AN14 usato come ingresso analogico 
  ADPCFGbits.PCFG15 = 0; // Pin AN15 usato come ingresso analogico 
	
	//ADCON1bits.ADON=0;
	
	//ADPCFG = 0x0003;  // tutte le porte B sono Analog Mode
	
	ADCON1 = 0x00E0; //Risultato Conversione Restituito come Integer
	
	ADCHS = ANALOGCHANNEL_7;   // Analog Channel OPTION
	
	ADCSSL = 0;
	ADCON3 = 0x1F02; // Sample time = 31Tad, Tad = internal 2 Tcy
	ADCON2 = 0x0000; // Use External Reference for AD Converter
	
	ADCON1bits.ADON = 1; // Convertitore Analogico Attivo

 // Configurazione CAN

  C1CTRLbits.REQOP = 4; // Configuration Mode
 
  while(C1CTRLbits.OPMODE != 4); // Attendi di essere entrato in Configuration Mode

  C1CTRLbits.CANCKS = 0; // Settaggio Frequenza CAN Master Clock 0 = 4 * FCY  1 = FCY 
							 
  C1CFG1bits.SJW = 0; //Synchronized Jump Width time O = 1 x TQ 

  C1CFG1bits.BRP = 0; //Baud Rate Prescaler

  C1CFG2 = 0x06D3; // SEG1PH, SEG2PH, PRSEG, Campionamento Bit 

 // I valori di BRP e di C1CFG2 determinano il Baud Rate alcuni valori sono:
 // BRP = 0 C1CFG2 = 0x06D3 ---> 1000 Kbits
 // BRP = 1 C1CFG2 = 0x06D3 --->  500 Kbits
 // BRP = 4 C1CFG2 = 0x05CA --->  250 Kbits
 // BRP = 6 C1CFG2 = 0x07DB --->  125 Kbits

  C1INTF = 0; //Reset tutte le interrupt generate dal CAN 
  IFS1bits.C1IF = 0; //Reset tutti i Flag dell Interrupt Status Register
  C1INTE = 0x00FF; //Abilita Tutte le Interrupt 
  IEC1bits.C1IE = 1;	//rende attive le Interrupt abilitate qui sopra 
 
  // Acceptance Filter

  C1RX0CON = 0x0004; // Se RX0 è pieno allora usa RX1
  C1RX1CON = 0x0000; // Inizializza RX1
  C1RXF0SID = 0x0000; // Inizializzazione Filtro ID Messaggi 
  C1RXF1SID = 0x1408;
  C1RXF2SID = 0x1408;
  C1RXF3SID = 0x1408;
  C1RXF4SID = 0x1408;
  C1RXF5SID = 0x1408;
  C1RXM0SID = 0x0001; // 0x1FFD se voglio considerare tutti i bit nel filtro 0x0001 se voglio accettare tutti i messaggi 
  C1RXM1SID = 0x0001; // 0x1FFD se voglio considerare tutti i bit nel filtro 0x0001 se voglio accettare tutti i messaggi

  C1TX0CON = 0x0001;  // Priorità messaggi medio bassa 0 = Bassa 1 = Medio Bassa 2 = Medio Alta 3 = Alta

  IDCAN_ByteL = CAN_OUT_ID_1 & 255; 
  IDCAN_ByteH = CAN_OUT_ID_1 >> 8; 

  Register_CAN_ID_L = (( IDCAN_ByteL & 0x3F ) << 2 );
  Register_CAN_ID_H = (( IDCAN_ByteL & 0xC0 ) >> 3 ) | (( IDCAN_ByteH & 0x07 ) << 5 );

  C1TX0SID = ( Register_CAN_ID_H << 8 ) | Register_CAN_ID_L;

  C1TX0EID = 0x0000;     // EID 
  C1TX0DLC = 0x01C0;	 // 8 byte di dati  
  
  C1TX0DLCbits.TXRB1 = 0;
  C1TX0DLCbits.TXRB0 = 0;
  C1TX0SIDbits.TXIDE = 0;
  C1TX1DLCbits.TXRB1 = 0;
  C1TX1DLCbits.TXRB0 = 0;
  C1TX1SIDbits.TXIDE = 0;
  C1TX2DLCbits.TXRB1 = 0;
  C1TX2DLCbits.TXRB0 = 0;
  C1TX2SIDbits.TXIDE = 0; 
  
  // Inizializzazione TX Buffer 

  C1TX0B1 = ( Can_Tx_Buffer [1] << 8 ) + Can_Tx_Buffer [0] ;
  C1TX0B2 = ( Can_Tx_Buffer [3] << 8 ) + Can_Tx_Buffer [2] ;
  C1TX0B3 = ( Can_Tx_Buffer [5] << 8 ) + Can_Tx_Buffer [4] ;
  C1TX0B4 = ( Can_Tx_Buffer [7] << 8 ) + Can_Tx_Buffer [6] ;

  C1CTRLbits.REQOP = 0; // Normal Mode
 
  while(C1CTRLbits.OPMODE != 0);// Attendi di essere entrato in Normal Mode
 
//*****************************************************  
  
 
 // Configurazione Timer 1

  TMR1 = 0; // Azzera Timer 1
  PR1 = 0x001D; // 0x0125 interrupt every 10ms 0x001D interrupt 1ms
  IFS0bits.T1IF = 0;	// clr interrupt flag
  IEC0bits.T1IE = 1;	// set interrupt enable bit
  T1CON = 0x8030; // Fosc/4, 1:256 prescale, start TMR1
  
//*****************************************************  
  
// Configurazione Seriale 1 
  
  U1BRG  = 0x0003; //0x0003 = 153600 0x0030=9600 0x0081 = 3600 0x00C1 = 2400 Bit per second
  U1MODE = 0x8400; // Abilita la porta UART 1 e la setta 8 bit dati nessuna parità 1 bit di stop e per l'utilizzo dei pin ausiliari
  U1STA  = 0x0400; // Reset status register and enable TX & RX

  _U1RXIF=0;	
  _U1RXIP = 7; // priorità bassa
  _U1RXIE = 1; // Abilita UART Receive interrupt
  
//****************************************************
  
 
  

 // Inizializzazione Variabili
    
 
     
 while(1)
  {
   if ( ActionNeeded ) // E' arrivato l'ID CAN che triggera un evento devo fare qualcosa
    {
     ActionNeeded = FALSE;  
     if ( CANMatrix[ 0 ][ 0 ] == 0xF0 ) // E' arrivato il comando per mettere il display in modalità aggiornamento 
      { 
       BufferUART1Count = 0; // Resetto il contatore della ricezione seriale   
       ResetBufferUART1 ( );
       for ( i = 0; i < MAXRAMBLOCKDIM; i++ ) BloccoRAM[ i ] = 0;
       CommandSend = 0xF0;
       SettaModalitaAggiornamento( );  
      } 
     else if ( CANMatrix[ 0 ][ 0 ] <= 0xEE ) // Carico i dati nel blocco RAM
      {
       CommandSend = 0x00;  
       Offset_RAM_Block = CANMatrix[ 0 ][ 0 ];  
       BloccoRAM[ Offset_RAM_Block ] = CANMatrix[ 0 ][ 1 ];
       BloccoRAM[ Offset_RAM_Block + 1 ] = CANMatrix[ 0 ][ 2 ];
       BloccoRAM[ Offset_RAM_Block + 2 ] = CANMatrix[ 0 ][ 3 ];
       BloccoRAM[ Offset_RAM_Block + 3 ] = CANMatrix[ 0 ][ 4 ];
       BloccoRAM[ Offset_RAM_Block + 4 ] = CANMatrix[ 0 ][ 5 ];
       BloccoRAM[ Offset_RAM_Block + 5 ] = CANMatrix[ 0 ][ 6 ];
       BloccoRAM[ Offset_RAM_Block + 6 ] = CANMatrix[ 0 ][ 7 ];
       AnswerNeeded = TRUE;  
      }  
     else if ( CANMatrix[ 0 ][ 0 ] == 0xF1 ) // E' arrivato il comando per scrivere il blocco nella RAM
      {
       BufferUART1Count = 0; // Resetto il contatore della ricezione seriale    
       ResetBufferUART1 ( );
       CommandSend = 0xF1;
       Indirizzo_RAM = CANMatrix[ 0 ][ 2 ]*256 + CANMatrix[ 0 ][ 3 ]; 
       Lunghezza_Blocco = CANMatrix[ 0 ][ 1 ];       
       ScriviBloccoRAM( Indirizzo_RAM, BloccoRAM, Lunghezza_Blocco );     
      }   
     else if ( ( CANMatrix[ 0 ][ 0 ] == 0xF2 ) && ( CommandSend != 0xF2 ) )// E' arrivato il comando per copiare la RAM nelle FLASH del DWin e controllo che non abbia già eseguito l'operazione necessaria
      {
       BufferUART1Count = 0; // Resetto il contatore della ricezione seriale  
       ResetBufferUART1 ( );
       RAM_to_Flash_Copy_Status = 0; // Primo stato dell'automa
       CommandSend = 0xF2;
       Indirizzo_FLASH = CANMatrix[ 0 ][ 1 ]*256 + CANMatrix[ 0 ][ 2 ]; 
       CopiaRAMinFLASH( Indirizzo_FLASH );     
      }   
     else if ( RAM_to_Flash_Copy_Status == 1 )
      {
       BufferUART1Count = 0; // Resetto il contatore della ricezione seriale  
       ResetBufferUART1 ( );
       RAM_to_Flash_Copy_Status = 2;    
       InterrogaSchermo ( );  
      }  
     else if ( CANMatrix[ 0 ][ 0 ] == 0xF3 ) // E' arrivato il comando per resettare lo schermo
      {
       BufferUART1Count = 0; // Resetto il contatore della ricezione seriale  
       ResetBufferUART1 ( );
       CommandSend = 0xF3;
       ResettaSchermo();
      }   
    }   
  }
	
}



// ---------------------FUNZIONI-------------------------- //
void Wait( int Hundreds )
 {
  Delay = Hundreds;
  while ( Delay > 0 )
   {
   }
 }	

void ResetBufferUART1 ( void )
 {
   unsigned char b=0;
   
   for( b=0; b < MAXUARTBUFFERDIM; b++ ) BufferUART1[ b ] = 0;
  
 }


void SettaModalitaAggiornamento( void )
// Mette Lo schermo DWIN in modalità aggiornamento
 {
  unsigned char CommandArr[10]={ 0x5A,0xA5,0x07,0x82,0x00,0xFC,0x55, 0xAA, 0x5A,0xA5 };
  
  unsigned char b=0;
  for(b=0;b<10;b++)
   {
    while(!U1STAbits.TRMT);
     U1TXREG =CommandArr[b];
   }
 }		

void ScriviBloccoRAM( unsigned int Indirizzo_RAM, unsigned char Blocco[ MAXRAMBLOCKDIM ], unsigned int Lunghezza_Blocco )
{
  unsigned char Comando[ MAXRAMBLOCKDIM + 4 ]={0x5A,0xA5,0x07,0x82,0x00,0x84,0x5A, 0x5A };
  unsigned char b=0;
   
  Comando[2] = Lunghezza_Blocco + 3; 
  Comando[4] = Indirizzo_RAM >> 8;
  Comando[5] = ( Indirizzo_RAM & 0x00FF );  
  
  for( b = 0; b < Lunghezza_Blocco; b++ )
   {
    Comando[ 6+b ] = Blocco[b]; 
   }  
  
  for( b=0; b < ( Lunghezza_Blocco + 6 ); b++ )
   {
    while(!U1STAbits.TRMT);
     U1TXREG =Comando[b];
   }   
}

void CopiaRAMinFLASH( unsigned int Indirizzo_FLASH )
{
  unsigned char Comando[ 18 ]={0x5A,0xA5,0x0F,0x82,0x00,0xAA,0x5A,0x02,0xBB,0xBB,0x80,0x00,0x00,0x00,0x00,0x00,0x00,0x00};
  unsigned char b=0;
   
  Comando[8] = Indirizzo_FLASH >> 8;
  Comando[9] = ( Indirizzo_FLASH & 0x00FF );  
  
  for( b=0; b < 18; b++ )
   {
    while(!U1STAbits.TRMT);
     U1TXREG =Comando[b];
   }   
}

void InterrogaSchermo( void )
{
  unsigned char Comando[ 7 ]={0x5A,0xA5,0x04,0x83,0x00,0xAA,0x01 };
  unsigned char b=0;
   
  Comando[8] = Indirizzo_FLASH >> 8;
  Comando[9] = ( Indirizzo_FLASH & 0x00FF );  
  
  for( b=0; b < 7; b++ )
   {
    while(!U1STAbits.TRMT);
     U1TXREG =Comando[b];
   }   
}

void ResettaSchermo( void )
{
  unsigned char Comando[ 10 ]={0x5A,0xA5,0x07,0x82,0x00,0x04,0x55,0xAA,0x5A,0xA5 };
  unsigned char b=0;
   
  Comando[8] = Indirizzo_FLASH >> 8;
  Comando[9] = ( Indirizzo_FLASH & 0x00FF );  
  
  for( b=0; b < 10; b++ )
   {
    while(!U1STAbits.TRMT);
     U1TXREG =Comando[b];
   }   
}

// -------------------------------------------------------------------------------------------------------------------




//--------------------------------------------------------------------------------------------------------------------------
											//Interrupt Section 
//--------------------------------------------------------------------------------------------------------------------------

void __attribute__((interrupt, no_auto_psv)) _U1RXInterrupt(void)
 {
   if (U1STAbits.OERR == 1)
     {
      U1STAbits.OERR = 0;
      // Clear Overrun Error to receive data
     }
    else if (U1STAbits.FERR == 1)
     {
      DataUART1 = U1RXREG;
      U1STAbits.FERR = 0;
      // Clear Frame Error to receive data
     }
    else if (U1STAbits.PERR == 1)
     {
      U1STAbits.PERR = 0;
     } 
    else 
     {
      while (U1STAbits.URXDA)
       {
        BufferUART1 [BufferUART1Count] = U1RXREG;
        BufferUART1Count++;
        if ( CommandSend == 0xF0 ) // Ho ricevuto via CAN il messaggio 0XF0 devo controllare che la risposta del display sia 5A A5 03 82 AF AB
         {   
          if ( ( BufferUART1[ BufferUART1Count - 2] == 0x4F ) && ( BufferUART1[ BufferUART1Count - 1] == 0x4B ) )
           {
            AnswerNeeded = TRUE;  
           }   
         }
        else if ( CommandSend == 0xF1 )
         {
          if ( ( BufferUART1[ BufferUART1Count - 2] == 0x4F ) && ( BufferUART1[ BufferUART1Count - 1] == 0x4B ) )
           {
            AnswerNeeded = TRUE;  
           }     
         }   
        else if ( CommandSend == 0xF2 )
         {
          if ( RAM_to_Flash_Copy_Status == 0 )  
           {   
            if ( ( BufferUART1[ BufferUART1Count - 2] == 0x4F ) && ( BufferUART1[ BufferUART1Count - 1] == 0x4B ) )
             {
              RAM_to_Flash_Copy_Status = 1;  
              ActionNeeded = TRUE;
             }     
           } 
          else if ( RAM_to_Flash_Copy_Status == 2 )  // Ho interrogato lo schermo e analizzo la risposta
           {
            if ( ( BufferUART1[ BufferUART1Count - 2] == 0x5A ) && ( BufferUART1[ BufferUART1Count - 1] == 0x02 ) )
             {
              // il Display non ha ancora finito di copiare la RAM nella Flash Rimetto l'automa un passo indietro ed interrogo ancora lo schermo  
              RAM_to_Flash_Copy_Status = 1;  
              contatore_automa++;
              ActionNeeded = TRUE;
             }  
            else if ( ( BufferUART1[ BufferUART1Count - 2] == 0x00 ) && ( BufferUART1[ BufferUART1Count - 1] == 0x02 ) )
             {
              // il Display ha finito di copiare la RAM nella Flash posso portare l'automa al passo successivo.  
              RAM_to_Flash_Copy_Status = 3;  
              AnswerNeeded = TRUE; 
             }   
           }   
         } 
        else if ( CommandSend == 0xF3 )
         {
          if ( ( BufferUART1[ BufferUART1Count - 2] == 0x4F ) && ( BufferUART1[ BufferUART1Count - 1] == 0x4B ) )
           {
            AnswerNeeded = TRUE;  
           }     
         }   
       }
    }

  _U1RXIF = 0; // clear RX interrupt flag
}

void __attribute__((interrupt, no_auto_psv)) _C1Interrupt(void)
{
      char k;
      
	  IFS1bits.C1IF = 0;         //Clear interrupt flag
         
      if (C1INTFbits.TX0IF)
       {
		C1INTFbits.TX0IF = 0;  // TX0 Interrupt 
       }  
      else if (C1INTFbits.TX1IF)
       {
		C1INTFbits.TX1IF = 0;  // TX1 Interrupt 
       }  

      if (C1INTFbits.RX0IF)
       {      
	    C1INTFbits.RX0IF = 0;   // RX0 Interrupt  

        MessageID = C1RX0SID >> 2;
        
        CANRow = MAXCANID-1;
        
        for ( k = 0; k < MAXCANID; k++ )
         {
          if ( CAN_IN_ID[ k ] == MessageID )  
           {
            CANRow = k;
            CAN_Time_Out = 0;  
            if ( MessageID == CAN_IN_TRIGGER_ID )
             {
              ActionNeeded = TRUE; // E' arrivato l'ID che richiede di svolgere una qualche azione
             }
           }   
         }
            
        CANMatrix[ CANRow ][ 0 ] = C1RX0B1 & 255;
        CANMatrix[ CANRow ][ 1 ] = C1RX0B1 >> 8;
        CANMatrix[ CANRow ][ 2 ] = C1RX0B2 & 255;
        CANMatrix[ CANRow ][ 3 ] = C1RX0B2 >> 8;
        CANMatrix[ CANRow ][ 4 ] = C1RX0B3 & 255;
        CANMatrix[ CANRow ][ 5 ] = C1RX0B3 >> 8;
        CANMatrix[ CANRow ][ 6 ] = C1RX0B4 & 255;
        CANMatrix[ CANRow ][ 7 ] = C1RX0B4 >> 8;
      
        C1RX0CONbits.RXFUL = 0;
       }
      else if (C1INTFbits.RX1IF)
       {      
	    C1INTFbits.RX1IF = 0;  	// RX1 Interrupt

        MessageID = C1RX1SID >> 2;
        
        CANRow = MAXCANID-1;
        
        for ( k = 0; k < MAXCANID; k++ )
         {
          if ( CAN_IN_ID[ k ] == MessageID )  
           {
            CANRow = k;
            CAN_Time_Out = 0;  
            if ( MessageID == CAN_IN_TRIGGER_ID ) 
             {
              ActionNeeded = TRUE; // E' arrivato l'ID che richiede di svolgere una qualche azione
             }
           }   
         }

        CANMatrix[ CANRow ][ 0 ] = C1RX1B1 & 255;
        CANMatrix[ CANRow ][ 1 ] = C1RX1B1 >> 8;
        CANMatrix[ CANRow ][ 2 ] = C1RX1B2 & 255;
        CANMatrix[ CANRow ][ 3 ] = C1RX1B2 >> 8;
        CANMatrix[ CANRow ][ 4 ] = C1RX1B3 & 255;
        CANMatrix[ CANRow ][ 5 ] = C1RX1B3 >> 8;
        CANMatrix[ CANRow ][ 6 ] = C1RX1B4 & 255;
        CANMatrix[ CANRow ][ 7 ] = C1RX1B4 >> 8;
      
        C1RX1CONbits.RXFUL = 0;
       }
     
      if (C1INTFbits.ERRIF)  // ERROR Interrupt
       {
        C1INTFbits.ERRIF = 0;
       }
}




void __attribute__((interrupt, no_auto_psv)) _T1Interrupt(void)
{
  
  IFS0bits.T1IF = 0; // Clear Interrupt Flag
  
  Delay--;
   if ( Delay < 0  ) Delay = 0;
  
  
  CAN_Time_Out++;
  
  if ( CAN_Time_Out > MAX_CAN_TIME_OUT )
   {
    CAN_Time_Out = MAX_CAN_TIME_OUT;
    
   }   
  else
   {   
    
   } 
  
 
     
  
  // ContatoreCAN_Message1++; 

  ContatoreCAN_Message2++;  
 
  LiveCounter++;
 
  if ( AnswerNeeded ) // E' arrivato il messaggio che triggera la risposta 
   {    
    if  ( C1TX0CONbits.TXREQ == 0 ) 
     {

      IDCAN_ByteL = CAN_OUT_ID_1 & 255; 
      IDCAN_ByteH = CAN_OUT_ID_1 >> 8; 

      Register_CAN_ID_L = (( IDCAN_ByteL & 0x3F ) << 2 );
      Register_CAN_ID_H = (( IDCAN_ByteL & 0xC0 ) >> 3 ) | (( IDCAN_ByteH & 0x07 ) << 5 );

      C1TX0SID = ( Register_CAN_ID_H << 8 ) | Register_CAN_ID_L;    
     
      if ( ( CANMatrix[ 0 ][ 0 ] ) >= 0xF0 ) 
       DataMessageNumber = CANMatrix[ 0 ][ 1 ] * 0x1000000 + CANMatrix[ 0 ][ 2 ] * 0x10000 + CANMatrix[ 0 ][ 3 ] * 0x100 + CANMatrix[ 0 ][ 4 ];  
        
      Can_Tx_Buffer [0] = CANMatrix[ 0 ][ 0 ];
      Can_Tx_Buffer [1] = CANMatrix[ 0 ][ 1 ];
      Can_Tx_Buffer [2] = CANMatrix[ 0 ][ 2 ]; 
      Can_Tx_Buffer [3] = CANMatrix[ 0 ][ 3 ]; 
      Can_Tx_Buffer [4] = CANMatrix[ 0 ][ 4 ]; 
      Can_Tx_Buffer [5] = CANMatrix[ 0 ][ 5 ]; 
      Can_Tx_Buffer [6] = CANMatrix[ 0 ][ 6 ];
      Can_Tx_Buffer [7] = CANMatrix[ 0 ][ 7 ];
     
      C1TX0B1 = ( Can_Tx_Buffer [1] << 8 ) + Can_Tx_Buffer [0];
      C1TX0B2 = ( Can_Tx_Buffer [3] << 8 ) + Can_Tx_Buffer [2] ;
      C1TX0B3 = ( Can_Tx_Buffer [5] << 8 ) + Can_Tx_Buffer [4] ;
      C1TX0B4 = ( Can_Tx_Buffer [7] << 8 ) + Can_Tx_Buffer [6] ;

      C1TX0CONbits.TXREQ = 1; // Avvia trasmissione CAN  
     } 
    //ContatoreCAN_Message1 = 0;  
    AnswerNeeded = FALSE;
   }

  if ( ContatoreCAN_Message2 == MAXCONTATORECAN_MESSAGE2 ) // 0x604
   {    
    if  ( C1TX0CONbits.TXREQ == 0 ) 
     {

      IDCAN_ByteL = CAN_OUT_ID_2 & 255; 
      IDCAN_ByteH = CAN_OUT_ID_2 >> 8; 

      Register_CAN_ID_L = (( IDCAN_ByteL & 0x3F ) << 2 );
      Register_CAN_ID_H = (( IDCAN_ByteL & 0xC0 ) >> 3 ) | (( IDCAN_ByteH & 0x07 ) << 5 );

      C1TX0SID = ( Register_CAN_ID_H << 8 ) | Register_CAN_ID_L;    

      Can_Tx_Buffer [0] =  contatore_automa;
      Can_Tx_Buffer [1] = 0;
      Can_Tx_Buffer [2] = 0;
      Can_Tx_Buffer [3] = 0; 
      Can_Tx_Buffer [4] = 0; 
      Can_Tx_Buffer [5] = 0;
      Can_Tx_Buffer [6] = FIRMWAREVERSION;
      Can_Tx_Buffer [7] = LiveCounter;
     
      C1TX0B1 = ( Can_Tx_Buffer [1] << 8 ) + Can_Tx_Buffer [0];
      C1TX0B2 = ( Can_Tx_Buffer [3] << 8 ) + Can_Tx_Buffer [2] ;
      C1TX0B3 = ( Can_Tx_Buffer [5] << 8 ) + Can_Tx_Buffer [4] ;
      C1TX0B4 = ( Can_Tx_Buffer [7] << 8 ) + Can_Tx_Buffer [6] ;

      C1TX0CONbits.TXREQ = 1; // Avvia trasmissione CAN  
     } 
    ContatoreCAN_Message2 = 0;  
   }
  
  
 
}