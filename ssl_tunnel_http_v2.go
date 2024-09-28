package main

import (
	"crypto/tls"
	"encoding/binary"
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
	bufferSize     int
	useHTTP        bool
)

func init() {
	flag.StringVar(&serverIP, "server-ip", "", "Server IP address")
	flag.StringVar(&serverPort, "server-port", "", "Server port")
	flag.StringVar(&localIP, "local-ip", "", "Local IP address")
	flag.StringVar(&localPort, "local-port", "", "Local port")
	flag.StringVar(&clientCertPath, "cert", "", "Path to client certificate")
	flag.StringVar(&clientKeyPath, "key", "", "Path to client key")
	flag.StringVar(&logFilePath, "log", "ssl_tunnel.log", "Path to log file")
	flag.IntVar(&bufferSize, "buffer", 4096, "Buffer size for data transfer")
	flag.BoolVar(&useHTTP, "http", false, "Use HTTP/HTTPS instead of raw TCP")
}

func main() {
	flag.Parse()

	if serverIP == "" || serverPort == "" || localIP == "" || localPort == "" || clientCertPath == "" || clientKeyPath == "" {
		log.Fatal("All parameters must be provided. Use -h for help.")
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
	cert, err := tls.LoadX509KeyPair(clientCertPath, clientKeyPath)
	if err != nil {
		return fmt.Errorf("failed to load client certificate: %v", err)
	}

	config := &tls.Config{
		Certificates:       []tls.Certificate{cert},
		InsecureSkipVerify: true,
	}

	serverConn, err := tls.Dial("tcp", serverIP+":"+serverPort, config)
	if err != nil {
		return fmt.Errorf("failed to connect to server: %v", err)
	}
	defer serverConn.Close()

	log.Println("Connected to server and SSL handshake completed.")

	if !useHTTP {
		// Send the initial key for the original protocol
		key := []byte{0x00, 0x08, 0x00, 0x00, 0x00, 0x22, 0x4D, 0x00, 0x00, 0x00}
		if _, err = serverConn.Write(key); err != nil {
			return fmt.Errorf("failed to send initial key: %v", err)
		}
		log.Println("Sent initial key.")

		// Receive and parse the response
		response := make([]byte, 20)
		if _, err = io.ReadFull(serverConn, response); err != nil {
			return fmt.Errorf("failed to receive response from server: %v", err)
		}
		log.Printf("Received response from server: %x\n", response)

		tunnelPort := binary.BigEndian.Uint32(response[16:20])
		log.Printf("Server tunnel port: %d\n", tunnelPort)

		// Connect to the local service
		localConn, err := net.Dial("tcp", fmt.Sprintf("%s:%s", localIP, localPort))
		if err != nil {
			return fmt.Errorf("failed to connect to local service: %v", err)
		}
		defer localConn.Close()

		log.Printf("Connected to local service at %s:%s\n", localIP, localPort)

		// Forward traffic in both directions
		go io.Copy(serverConn, localConn)
		io.Copy(localConn, serverConn)
	} else {
		localListener, err := net.Listen("tcp", localIP+":"+localPort)
		if err != nil {
			return fmt.Errorf("failed to start local listener: %v", err)
		}
		defer localListener.Close()

		log.Printf("Listening on %s:%s\n", localIP, localPort)

		for {
			localConn, err := localListener.Accept()
			if err != nil {
				log.Printf("Error accepting connection: %v\n", err)
				continue
			}

			go handleConnection(localConn, serverConn)
		}
	}

	return nil
}

func handleConnection(localConn net.Conn, serverConn net.Conn) {
	defer localConn.Close()

	go io.Copy(serverConn, localConn)
	io.Copy(localConn, serverConn)
}