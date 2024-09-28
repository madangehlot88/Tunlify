package main

import (
	"crypto/tls"
	"flag"
	"fmt"
	"io"
	"log"
	"net"
	"os"
	"os/signal"
	"syscall"
	"time"
)

var (
	serverIP       string
	serverPort     string
	localIP        string
	localPort      string
	clientCertPath string
	clientKeyPath  string
	logFilePath    string
	useHTTP        bool
)

func init() {
	flag.StringVar(&serverIP, "server-ip", "", "Server IP address")
	flag.StringVar(&serverPort, "server-port", "", "Server port")
	flag.StringVar(&localIP, "local-ip", "127.0.0.1", "Local IP address")
	flag.StringVar(&localPort, "local-port", "80", "Local port")
	flag.StringVar(&clientCertPath, "cert", "", "Path to client certificate")
	flag.StringVar(&clientKeyPath, "key", "", "Path to client key")
	flag.StringVar(&logFilePath, "log", "ssl_tunnel.log", "Path to log file")
	flag.BoolVar(&useHTTP, "http", false, "Use HTTP mode for server connection")
}

func main() {
	flag.Parse()

	if serverIP == "" || serverPort == "" {
		log.Fatal("Server IP and port must be provided. Use -h for help.")
	}

	if !useHTTP && (clientCertPath == "" || clientKeyPath == "") {
		log.Fatal("Client certificate and key must be provided for non-HTTP mode. Use -h for help.")
	}

	// Set up logging
	logFile, err := os.OpenFile(logFilePath, os.O_APPEND|os.O_CREATE|os.O_WRONLY, 0644)
	if err != nil {
		log.Fatal(err)
	}
	defer logFile.Close()
	log.SetOutput(logFile)

	// Handle graceful shutdown
	sigChan := make(chan os.Signal, 1)
	signal.Notify(sigChan, syscall.SIGINT, syscall.SIGTERM)
	go func() {
		<-sigChan
		log.Println("Received shutdown signal. Closing tunnel...")
		os.Exit(0)
	}()

	log.Println("Starting SSL tunnel...")

	for {
		err := connectAndForward()
		if err != nil {
			log.Printf("Error: %v\n", err)
			log.Println("Retrying in 5 seconds...")
			time.Sleep(5 * time.Second)
		}
	}
}

func connectAndForward() error {
	var serverConn net.Conn
	var err error

	log.Printf("Attempting to connect to server at %s:%s...", serverIP, serverPort)

	config := &tls.Config{
		InsecureSkipVerify: true,
		MinVersion:         tls.VersionTLS12,
	}

	if !useHTTP {
		cert, err := tls.LoadX509KeyPair(clientCertPath, clientKeyPath)
		if err != nil {
			return fmt.Errorf("failed to load client certificate: %v", err)
		}
		config.Certificates = []tls.Certificate{cert}
	}

	serverConn, err = tls.Dial("tcp", fmt.Sprintf("%s:%s", serverIP, serverPort), config)
	if err != nil {
		return fmt.Errorf("failed to connect to server: %v", err)
	}
	defer serverConn.Close()

	log.Println("Connected to server successfully.")
	log.Printf("Using TLS version: %s", versionToString(serverConn.(*tls.Conn).ConnectionState().Version))

	localConn, err := net.Dial("tcp", fmt.Sprintf("%s:%s", localIP, localPort))
	if err != nil {
		return fmt.Errorf("failed to connect to local service: %v", err)
	}
	defer localConn.Close()

	log.Printf("Connected to local service at %s:%s", localIP, localPort)

	// Forward traffic in both directions
	errChan := make(chan error, 2)

	go func() {
		_, err := io.Copy(serverConn, localConn)
		errChan <- err
	}()

	go func() {
		_, err := io.Copy(localConn, serverConn)
		errChan <- err
	}()

	// Wait for an error or EOF from either direction
	err = <-errChan
	if err != nil && err != io.EOF {
		return fmt.Errorf("error during data transfer: %v", err)
	}

	log.Println("Connection closed")
	return nil
}

func versionToString(version uint16) string {
	switch version {
	case tls.VersionTLS10:
		return "TLS 1.0"
	case tls.VersionTLS11:
		return "TLS 1.1"
	case tls.VersionTLS12:
		return "TLS 1.2"
	case tls.VersionTLS13:
		return "TLS 1.3"
	default:
		return fmt.Sprintf("Unknown (%d)", version)
	}
}